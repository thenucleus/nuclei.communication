﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Nuclei.Communication.Properties;
using Nuclei.Communication.Protocol.Messages;
using Nuclei.Diagnostics;
using Nuclei.Diagnostics.Logging;
using Nuclei.Diagnostics.Profiling;

namespace Nuclei.Communication.Protocol
{
    /// <summary>
    /// Handles incoming messages and dispatches them to the correct processors.
    /// </summary>
    internal sealed class MessageHandler : IDirectIncomingMessages, IProcessIncomingMessages
    {
        private static bool IsLastChanceProcessor(IMessageProcessAction notifyAction)
        {
            var attributes = notifyAction.GetType().GetCustomAttributes(typeof(LastChanceMessageHandlerAttribute), false);
            return attributes.Length == 1;
        }

        private static bool IsMessageIndicatingEndpointDisconnect(ICommunicationMessage message)
        {
            return message.GetType().Equals(typeof(EndpointDisconnectMessage));
        }

        /// <summary>
        /// The object used to lock on.
        /// </summary>
        private readonly object m_Lock = new object();

        /// <summary>
        /// The collection of filters that should be used more than once.
        /// </summary>
        /// <design>
        /// Could improve this if we store filters based on the type of message they work on. All filters that work
        /// on the same type get placed in a collection (with their connected actions). That way the look-up will
        /// be pretty quick. On the other hand we'll be storing a collection for each message type we filter on
        /// which may mean we're storing an entire Collection object for a single filter.
        /// </design>
        private readonly Dictionary<IMessageFilter, IMessageProcessAction> m_Filters
            = new Dictionary<IMessageFilter, IMessageProcessAction>();

        /// <summary>
        /// The collection that maps the ID numbers of the messages that are waiting for a response
        /// message to the endpoint.
        /// </summary>
        /// <remarks>
        /// We track the endpoint from which we're expecting a response in case we get an <c>EndpointDisconnectMessage</c>.
        /// In that case we need to know if we just lost the source of our potential answer or not.
        /// </remarks>
        private readonly Dictionary<MessageId, Tuple<EndpointId, Subject<ICommunicationMessage>, CancellationTokenSource>> m_TasksWaitingForResponse
            = new Dictionary<MessageId, Tuple<EndpointId, Subject<ICommunicationMessage>, CancellationTokenSource>>();

        /// <summary>
        /// The object that stores the endpoint information for all the endpoints that we are permitted to 
        /// communicate with.
        /// </summary>
        private readonly IStoreInformationAboutEndpoints m_Endpoints;

        /// <summary>
        /// The object that provides the diagnostics methods for the system.
        /// </summary>
        private readonly SystemDiagnostics m_Diagnostics;

        /// <summary>
        /// The object used for scheduling reactive extension tasks.
        /// </summary>
        private readonly IScheduler m_Scheduler;

        /// <summary>
        /// The processor that should be used if there are no other message processors for a message type.
        /// </summary>
        private IMessageProcessAction m_LastChanceProcessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageHandler"/> class.
        /// </summary>
        /// <param name="endpoints">
        /// The object that stores the endpoint information for all the endpoints that we are permitted to 
        /// communicate with.
        /// </param>
        /// <param name="systemDiagnostics">The object that provides the diagnostics methods for the system.</param>
        /// <param name="scheduler">The object used for scheduling reactive extension tasks.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="endpoints"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="systemDiagnostics"/> is <see langword="null" />.
        /// </exception>
        public MessageHandler(IStoreInformationAboutEndpoints endpoints, SystemDiagnostics systemDiagnostics, IScheduler scheduler = null)
        {
            {
                Lokad.Enforce.Argument(() => endpoints);
                Lokad.Enforce.Argument(() => systemDiagnostics);
            }

            m_Endpoints = endpoints;
            m_Diagnostics = systemDiagnostics;
            m_Scheduler = scheduler ?? Scheduler.Default;
        }

        /// <summary>
        /// On arrival of a message which responds to the message with the
        /// <paramref name="inResponseTo"/> ID number the caller will be
        /// able to get the message through the <see cref="Task{T}"/> object.
        /// </summary>
        /// <param name="messageReceiver">The ID of the endpoint to which the original message was send.</param>
        /// <param name="inResponseTo">The ID number of the message for which a response is expected.</param>
        /// <param name="timeout">The maximum amount of time the response operation is allowed to take.</param>
        /// <returns>
        /// A <see cref="Task{T}"/> implementation which returns the response message.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="messageReceiver"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="inResponseTo"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     Thrown if <paramref name="messageReceiver"/> is equal to <see cref="MessageId.None"/>.
        /// </exception>
        public Task<ICommunicationMessage> ForwardResponse(EndpointId messageReceiver, MessageId inResponseTo, TimeSpan timeout)
        {
            {
                Lokad.Enforce.Argument(() => messageReceiver);
                Lokad.Enforce.Argument(() => inResponseTo);
                Lokad.Enforce.With<ArgumentException>(!inResponseTo.Equals(MessageId.None), Resources.Exceptions_Messages_AMessageNeedsToHaveAnId);
            }

            lock (m_Lock)
            {
                if (!m_TasksWaitingForResponse.ContainsKey(inResponseTo))
                {
                    var subject = new Subject<ICommunicationMessage>();
                    var token = new CancellationTokenSource();
                    m_TasksWaitingForResponse.Add(inResponseTo, Tuple.Create(messageReceiver, subject, token));
                }

                var pair = m_TasksWaitingForResponse[inResponseTo];
                return pair.Item2
                    .Timeout(timeout, m_Scheduler)
                    .ToTask(pair.Item3.Token)
                    .ContinueWith(
                        t =>
                        {
                            CleanUpResponseHandlerFor(inResponseTo);
                            if (t.Exception != null)
                            {
                                throw new AggregateException(t.Exception.InnerExceptions);
                            }

                            if (t.IsCanceled)
                            {
                                return null;
                            }

                            return t.Result;
                        },
                        pair.Item3.Token,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Current);
            }
        }

        private void CleanUpResponseHandlerFor(MessageId message)
        {
            Tuple<EndpointId, Subject<ICommunicationMessage>, CancellationTokenSource> pair = null;
            lock (m_Lock)
            {
                if (m_TasksWaitingForResponse.ContainsKey(message))
                {
                    pair = m_TasksWaitingForResponse[message];
                    m_TasksWaitingForResponse.Remove(message);
                }
            }

            pair.Item2.Dispose();
            pair.Item3.Dispose();
        }

        /// <summary>
        /// On arrival of a message which passes the given filter the caller
        /// will be notified though the given delegate.
        /// </summary>
        /// <param name="messageFilter">The message filter.</param>
        /// <param name="notifyAction">The action invoked when a matching message arrives.</param>
        public void ActOnArrival(IMessageFilter messageFilter, IMessageProcessAction notifyAction)
        {
            {
                Lokad.Enforce.Argument(() => messageFilter);
                Lokad.Enforce.Argument(() => notifyAction);
            }

            lock (m_Lock)
            {
                if (IsLastChanceProcessor(notifyAction))
                {
                    m_LastChanceProcessor = notifyAction;
                    return;
                }

                if (!m_Filters.ContainsKey(messageFilter))
                {
                    m_Filters.Add(messageFilter, notifyAction);
                }
            }
        }

        /// <summary>
        /// Handles the case that a remote endpoint has disconnected.
        /// </summary>
        /// <param name="id">The ID of the remote endpoint.</param>
        public void OnEndpointSignedOff(EndpointId id)
        {
            {
                Lokad.Enforce.Argument(() => id);
            }

            m_Diagnostics.Log(
                LevelToLog.Trace,
                CommunicationConstants.DefaultLogTextPrefix,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Endpoint {0} has signed off.",
                    id));

            lock (m_Lock)
            {
                var messagesThatWillNotBeAnswered = new List<MessageId>();
                foreach (var pair in m_TasksWaitingForResponse)
                {
                    if (pair.Value.Item1.Equals(id))
                    {
                        messagesThatWillNotBeAnswered.Add(pair.Key);
                    }
                }

                foreach (var messageId in messagesThatWillNotBeAnswered)
                {
                    var pair = m_TasksWaitingForResponse[messageId];
                    pair.Item3.Cancel();
                }
            }
        }

        /// <summary>
        /// Processes the message and invokes the desired functions based on the 
        /// message contents or type.
        /// </summary>
        /// <param name="message">The message that should be processed.</param>
        public void ProcessMessage(ICommunicationMessage message)
        {
            {
                Lokad.Enforce.Argument(() => message);
            }

            using (m_Diagnostics.Profiler.Measure(CommunicationConstants.TimingGroup, "MessageHandler: processing message"))
            {
                // Confirm that the endpoint is still alive.
                RaiseOnConfirmChannelIntegrity(message.Sender);

                // First check that the message isn't a response. If it is then we see if we have a
                // waiting task for that. If not then the message will just disappear in the void
                if (!message.InResponseTo.Equals(MessageId.None))
                {
                    m_Diagnostics.Log(
                        LevelToLog.Trace,
                        CommunicationConstants.DefaultLogTextPrefix,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Message [{0}] is a response to message [{1}].",
                            message.Id,
                            message.InResponseTo));

                    using (m_Diagnostics.Profiler.Measure(CommunicationConstants.TimingGroup, "Processing response message"))
                    {
                        Tuple<EndpointId, Subject<ICommunicationMessage>, CancellationTokenSource> source = null;
                        lock (m_Lock)
                        {
                            if (m_TasksWaitingForResponse.ContainsKey(message.InResponseTo))
                            {
                                source = m_TasksWaitingForResponse[message.InResponseTo];
                            }
                        }

                        // Invoke the OnNext and OnCompleted outside the lock because the setting of the 
                        // result may lead to other messages being send and more responses 
                        // being required to be handled. All of that may need access to the lock.
                        if (source != null)
                        {
                            source.Item2.OnNext(message);
                            source.Item2.OnCompleted();
                        }

                        return;
                    }
                }

                // Need to do message filtering here
                // Only accept messages from endpoints that we know of (and have accepted) or
                // messages that are handshakes / responses to handshakes
                if (!ShouldProcessMessage(message))
                {
                    return;
                }

                using (m_Diagnostics.Profiler.Measure(CommunicationConstants.TimingGroup, "Using message filters"))
                {
                    // The message isn't a response so go to the filters
                    // First copy the filters and their associated actions so that we can
                    // invoke the actions outside the lock. This is necessary because
                    // a message might invoke the requirement for more messages and eventual
                    // responses. Setting up a response requires that we can take out the lock
                    // again.
                    // Once we have the filters then we can just run past all of them and invoke
                    // the actions based on the filter pass through.
                    Dictionary<IMessageFilter, IMessageProcessAction> localCollection;
                    lock (m_Lock)
                    {
                        localCollection = new Dictionary<IMessageFilter, IMessageProcessAction>(m_Filters);
                    }

                    foreach (var pair in localCollection)
                    {
                        if (pair.Key.PassThrough(message))
                        {
                            m_Diagnostics.Log(
                                LevelToLog.Trace,
                                CommunicationConstants.DefaultLogTextPrefix,
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "Processing message of type {0} with action of type {1}.",
                                    message.GetType(),
                                    pair.Value.GetType()));

                            pair.Value.Invoke(message);

                            // Each message type should only be procesed by one process action
                            // so if we find it, then we're done.
                            return;
                        }
                    }
                }

                // The message type is unknown. See if the last chance handler wants it ...
                if (m_LastChanceProcessor != null)
                {
                    m_LastChanceProcessor.Invoke(message);
                }
            }
        }

        private bool ShouldProcessMessage(ICommunicationMessage message)
        {
            return m_Endpoints.CanCommunicateWithEndpoint(message.Sender)
                || (message.IsHandshake() || IsMessageIndicatingEndpointDisconnect(message));
        }

        /// <summary>
        /// Handles the case that the local channel, from which the input messages are send,
        /// is closed.
        /// </summary>
        public void OnLocalChannelClosed()
        {
            lock (m_Lock)
            {
                // No single message will get a response anymore. 
                // Nuke them all
                var tuples = m_TasksWaitingForResponse.Values.ToList();
                foreach (var pair in tuples)
                {
                    pair.Item3.Cancel();
                }
            }
        }

        /// <summary>
        /// An event raised when data is received from a remote endpoint.
        /// </summary>
        public event EventHandler<EndpointEventArgs> OnConfirmChannelIntegrity;

        private void RaiseOnConfirmChannelIntegrity(EndpointId endpoint)
        {
            var local = OnConfirmChannelIntegrity;
            if (local != null)
            {
                local(this, new EndpointEventArgs(endpoint));
            }
        }
    }
}
