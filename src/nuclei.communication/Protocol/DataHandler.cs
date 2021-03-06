﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Nuclei.Diagnostics;
using Nuclei.Diagnostics.Logging;
using Nuclei.Diagnostics.Profiling;

namespace Nuclei.Communication.Protocol
{
    internal sealed class DataHandler : IDirectIncomingData, IProcessIncomingData
    {
        /// <summary>
        /// The object used to lock on.
        /// </summary>
        private readonly object m_Lock = new object();

        /// <summary>
        /// The collection that maps the ID numbers of the endpoint that are waiting for a data stream from the endpoint.
        /// </summary>
        private readonly Dictionary<EndpointId, Tuple<string, Subject<FileInfo>, CancellationTokenSource>> m_TasksWaitingForData
            = new Dictionary<EndpointId, Tuple<string, Subject<FileInfo>, CancellationTokenSource>>();

        /// <summary>
        /// The object that provides the diagnostics methods for the system.
        /// </summary>
        private readonly SystemDiagnostics m_Diagnostics;

        /// <summary>
        /// The object used for scheduling reactive extension tasks.
        /// </summary>
        private readonly IScheduler m_Scheduler;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataHandler"/> class.
        /// </summary>
        /// <param name="diagnostics">The object that provides the diagnostics methods for the system.</param>
        /// <param name="scheduler">The object used for scheduling reactive extension tasks.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="diagnostics"/> is <see langword="null" />.
        /// </exception>
        public DataHandler(SystemDiagnostics diagnostics, IScheduler scheduler = null)
        {
            {
                Lokad.Enforce.Argument(() => diagnostics);
            }

            m_Diagnostics = diagnostics;
            m_Scheduler = scheduler ?? Scheduler.Default;
        }

        /// <summary>
        /// On arrival of data from the given <paramref name="messageReceiver"/> the caller will be
        /// able to get the data stream from disk through the <see cref="Task{T}"/> object.
        /// </summary>
        /// <param name="messageReceiver">The ID of the endpoint to which the original message was send.</param>
        /// <param name="filePath">The full path to the file to which the data stream should be written.</param>
        /// <param name="timeout">The maximum amount of time the response operation is allowed to take.</param>
        /// <returns>
        /// A <see cref="Task{T}"/> implementation which returns the full path of the file which contains the data stream.
        /// </returns>
        public Task<FileInfo> ForwardData(EndpointId messageReceiver, string filePath, TimeSpan timeout)
        {
            {
                Lokad.Enforce.Argument(() => messageReceiver);
            }

            lock (m_Lock)
            {
                if (!m_TasksWaitingForData.ContainsKey(messageReceiver))
                {
                    var source = new Subject<FileInfo>();
                    var token = new CancellationTokenSource();
                    m_TasksWaitingForData.Add(messageReceiver, Tuple.Create(filePath, source, token));
                }

                var pair = m_TasksWaitingForData[messageReceiver];
                return pair.Item2
                    .Timeout(timeout, m_Scheduler)
                    .ToTask(pair.Item3.Token)
                    .ContinueWith(
                        t =>
                        {
                            CleanUpResponseHandlerFor(messageReceiver);
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

        private void CleanUpResponseHandlerFor(EndpointId endpoint)
        {
            Tuple<string, Subject<FileInfo>, CancellationTokenSource> pair = null;
            lock (m_Lock)
            {
                if (m_TasksWaitingForData.ContainsKey(endpoint))
                {
                    pair = m_TasksWaitingForData[endpoint];
                    m_TasksWaitingForData.Remove(endpoint);
                }
            }

            pair.Item2.Dispose();
            pair.Item3.Dispose();
        }

        /// <summary>
        /// Processes the data and invokes the desired functions based on the pre-registered information.
        /// </summary>
        /// <param name="message">The message that should be processed.</param>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "We just pass on the exception to the task.")]
        public void ProcessData(DataTransferMessage message)
        {
            {
                Lokad.Enforce.Argument(() => message);
            }

            m_Diagnostics.Log(
                LevelToLog.Trace,
                CommunicationConstants.DefaultLogTextPrefix,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Received data stream from {0}.",
                    message.SendingEndpoint));

            using (m_Diagnostics.Profiler.Measure(CommunicationConstants.TimingGroup, "Processing incoming data stream"))
            {
                Tuple<string, Subject<FileInfo>, CancellationTokenSource> pair = null;
                lock (m_Lock)
                {
                    if (m_TasksWaitingForData.ContainsKey(message.SendingEndpoint))
                    {
                        pair = m_TasksWaitingForData[message.SendingEndpoint];
                    }
                }

                // Invoke the SetResult outside the lock because the setting of the 
                // result may lead to other messages being send and more responses 
                // being required to be handled. All of that may need access to the lock.
                if (pair != null)
                {
                    try
                    {
                        var directory = Path.GetDirectoryName(pair.Item1);
                        if ((directory != null) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        using (var fileStream = new FileStream(pair.Item1, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            message.Data.CopyTo(fileStream);
                        }

                        pair.Item2.OnNext(new FileInfo(pair.Item1));
                        pair.Item2.OnCompleted();
                    }
                    catch (Exception e)
                    {
                        pair.Item2.OnError(e);
                    }
                }
            }
        }

        /// <summary>
        /// Handles the case that an endpoint signs off.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint that has signed off.</param>
        public void OnEndpointSignedOff(EndpointId endpoint)
        {
            lock (m_Lock)
            {
                if (m_TasksWaitingForData.ContainsKey(endpoint))
                {
                    var pair = m_TasksWaitingForData[endpoint];
                    pair.Item3.Cancel();
                }
            }
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
                var tuples = m_TasksWaitingForData.Values.ToList();
                foreach (var pair in tuples)
                {
                    pair.Item3.Cancel();
                }
            }
        }
    }
}
