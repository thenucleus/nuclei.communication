﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using Nuclei.Communication.Protocol.Messages;

namespace Nuclei.Communication.Protocol
{
    /// <summary>
    /// Defines the methods required for handling communication with other Apollo applications 
    /// across the network.
    /// </summary>
    /// <remarks>
    /// The design of this class assumes that there is only one of these active for a given
    /// channel template (e.g. TCP) at any given time.
    /// This is because the current class has a receiving endpoint of which there can only 
    /// be one. If there are multiple communication channels sharing the receiving endpoint then
    /// we don't know which channel should get the messages.
    /// </remarks>
    internal sealed class ProtocolChannel : IProtocolChannel, IDisposable
    {
        /// <summary>
        /// The object used to lock on.
        /// </summary>
        private readonly object m_Lock = new object();

        /// <summary>
        /// The collection that stores the hosts based on the version of the 
        /// protocol layer they provide.
        /// </summary>
        private readonly Dictionary<Version, IHoldServiceConnections> m_MessageHostsByVersion
            = new Dictionary<Version, IHoldServiceConnections>();

        /// <summary>
        /// The collection that stores the hosts based on the version of the 
        /// protocol layer they provide.
        /// </summary>
        private readonly Dictionary<Version, IHoldServiceConnections> m_DataHostsByVersion
            = new Dictionary<Version, IHoldServiceConnections>();

        /// <summary>
        /// The collection that stores the message pipes based on the version of the 
        /// protocol layer that they handle.
        /// </summary>
        private readonly Dictionary<Version, Tuple<IMessagePipe, EventHandler<MessageEventArgs>>> m_MessagePipesByVersion
            = new Dictionary<Version, Tuple<IMessagePipe, EventHandler<MessageEventArgs>>>();

        /// <summary>
        /// The collection that stores the data pipes based on the version of the 
        /// protocol layer that they handle.
        /// </summary>
        private readonly Dictionary<Version, Tuple<IDataPipe, EventHandler<DataTransferEventArgs>>> m_DataPipesByVersion
            = new Dictionary<Version, Tuple<IDataPipe, EventHandler<DataTransferEventArgs>>>();

        /// <summary>
        /// The collection that contains the sending endpoints based on the version of the protocol layer that
        /// they handle.
        /// </summary>
        private readonly Dictionary<Version, ISendingEndpoint> m_SendingEndpoints
            = new Dictionary<Version, ISendingEndpoint>();

        /// <summary>
        /// The collection that contains the connection information for all the local channels.
        /// </summary>
        private readonly List<ProtocolInformation> m_LocalConnectionPoints
            = new List<ProtocolInformation>();

        /// <summary>
        /// The ID number of the current endpoint.
        /// </summary>
        private readonly EndpointId m_Id;

        /// <summary>
        /// Indicates the type of channel that we're dealing with and provides
        /// utility methods for the channel.
        /// </summary>
        private readonly IProtocolChannelTemplate m_Template;

        /// <summary>
        /// The function that creates the host information for the discovery host.
        /// </summary>
        private readonly Func<IHoldServiceConnections> m_HostBuilder;

        /// <summary>
        /// The function used to build message pipes.
        /// </summary>
        private readonly Func<Version, Tuple<Type, IMessagePipe>> m_MessageMessageReceiverBuilder;

        /// <summary>
        /// The function used to build data pipes.
        /// </summary>
        private readonly Func<Version, Tuple<Type, IDataPipe>> m_DataReceiverBuilder;

        /// <summary>
        /// The function that is used to create a specific version of the the message sending endpoint 
        /// to connect to a given URL.
        /// </summary>
        private readonly Func<Version, Uri, IMessageSendingEndpoint> m_VersionedMessageSenderBuilder;

        /// <summary>
        /// The function that is used to create a specific version of the the data transferring endpoint 
        /// to connect to a given URL.
        /// </summary>
        private readonly Func<Version, Uri, IDataTransferingEndpoint> m_VersionedDataSenderBuilder;

        /// <summary>
        /// The function that generates sending endpoints.
        /// </summary>
        private readonly BuildSendingEndpoint m_SenderBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtocolChannel"/> class.
        /// </summary>
        /// <param name="id">The ID number of the current endpoint.</param>
        /// <param name="channelTemplate">The type of channel, e.g. TCP.</param>
        /// <param name="hostBuilder">
        /// The function that returns an object which handles the <see cref="ServiceHost"/> for the channel used to communicate with.
        /// </param>
        /// <param name="messageReceiverBuilder">The function that builds message receiving endpoints.</param>
        /// <param name="dataReceiverBuilder">The function that builds data receiving endpoints.</param>
        /// <param name="senderBuilder">The function that builds sending endpoints.</param>
        /// <param name="versionedMessageSenderBuilder">
        /// The function that creates a specific version of the message sender used to connect to a given URL.
        /// </param>
        /// <param name="versionedDataSenderBuilder">
        /// The function that creates a specific version of the data sender used to connect to a given URL.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="id"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="channelTemplate"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="hostBuilder"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="messageReceiverBuilder"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="dataReceiverBuilder"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="senderBuilder"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="versionedMessageSenderBuilder"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="versionedDataSenderBuilder"/> is <see langword="null" />.
        /// </exception>
        public ProtocolChannel(
            EndpointId id,
            IProtocolChannelTemplate channelTemplate,
            Func<IHoldServiceConnections> hostBuilder,
            Func<Version, Tuple<Type, IMessagePipe>> messageReceiverBuilder,
            Func<Version, Tuple<Type, IDataPipe>> dataReceiverBuilder,
            BuildSendingEndpoint senderBuilder,
            Func<Version, Uri, IMessageSendingEndpoint> versionedMessageSenderBuilder,
            Func<Version, Uri, IDataTransferingEndpoint> versionedDataSenderBuilder)
        {
            {
                Lokad.Enforce.Argument(() => id);
                Lokad.Enforce.Argument(() => channelTemplate);
                Lokad.Enforce.Argument(() => hostBuilder);
                Lokad.Enforce.Argument(() => messageReceiverBuilder);
                Lokad.Enforce.Argument(() => dataReceiverBuilder);
                Lokad.Enforce.Argument(() => senderBuilder);
                Lokad.Enforce.Argument(() => versionedMessageSenderBuilder);
                Lokad.Enforce.Argument(() => versionedDataSenderBuilder);
            }

            m_Id = id;
            m_Template = channelTemplate;
            m_HostBuilder = hostBuilder;

            m_MessageMessageReceiverBuilder = messageReceiverBuilder;
            m_DataReceiverBuilder = dataReceiverBuilder;

            m_SenderBuilder = senderBuilder;
            m_VersionedMessageSenderBuilder = versionedMessageSenderBuilder;
            m_VersionedDataSenderBuilder = versionedDataSenderBuilder;
        }

        /// <summary>
        /// Returns a collection containing the connection information for each of the available channels.
        /// </summary>
        /// <returns>The collection that contains the connection information for each of the available channels.</returns>
        public IEnumerable<ProtocolInformation> LocalConnectionPoints()
        {
            lock (m_Lock)
            {
                return m_LocalConnectionPoints.ToList();
            }
        }

        /// <summary>
        /// Returns the connection information for the channel that handles messages for the given version
        /// of the protocol.
        /// </summary>
        /// <param name="protocolVersion">The version of the protocol for which the protocol information should be returned.</param>
        /// <returns>The connection information for the channel that handles messages for the given version of the protocol.</returns>
        public ProtocolInformation LocalConnectionPointForVersion(Version protocolVersion)
        {
            {
                Lokad.Enforce.Argument(() => protocolVersion);
            }

            lock (m_Lock)
            {
                var result = m_LocalConnectionPoints.Find(p => p.Version == protocolVersion);
                return result;
            }
        }

        /// <summary>
        /// Opens the channel and provides information on how to connect to the given channel.
        /// </summary>
        public void OpenChannel()
        {
            lock (m_Lock)
            {
                foreach (var version in ProtocolVersions.SupportedVersions())
                {
                    if (m_MessagePipesByVersion.ContainsKey(version) || m_DataPipesByVersion.ContainsKey(version))
                    {
                        CloseChannel(version);
                    }

                    var dataUri = CreateAndStoreDataChannelForProtocolVersion(version);
                    var messageUri = CreateAndStoreMessageChannelForProtocolVersion(version);

                    var localConnection = new ProtocolInformation(version, messageUri, dataUri);
                    m_LocalConnectionPoints.Add(localConnection);
                }
            }
        }

        private Uri CreateAndStoreDataChannelForProtocolVersion(Version version)
        {
            var dataPair = m_DataReceiverBuilder(version);
            var dataType = dataPair.Item1;
            var dataPipe = dataPair.Item2;

            EventHandler<DataTransferEventArgs> dataHandler = (s, e) => RaiseOnDataReception(e.Data);
            dataPipe.OnNewData += dataHandler;
            m_DataPipesByVersion.Add(version, new Tuple<IDataPipe, EventHandler<DataTransferEventArgs>>(dataPipe, dataHandler));

            Func<ServiceHost, ServiceEndpoint> dataEndpointBuilder =
                h =>
                {
                    var dataEndpoint = m_Template.AttachDataEndpoint(h, dataType);
                    return dataEndpoint;
                };
            var host = m_HostBuilder();
            m_DataHostsByVersion.Add(version, host);

            return host.OpenChannel(dataPipe, dataEndpointBuilder);
        }

        private Uri CreateAndStoreMessageChannelForProtocolVersion(Version version)
        {
            var messagePair = m_MessageMessageReceiverBuilder(version);
            var messageType = messagePair.Item1;
            var messagePipe = messagePair.Item2;

            EventHandler<MessageEventArgs> messageHandler = (s, e) => RaiseOnMessageReception(e.Message);
            messagePipe.OnNewMessage += messageHandler;
            m_MessagePipesByVersion.Add(version, new Tuple<IMessagePipe, EventHandler<MessageEventArgs>>(messagePipe, messageHandler));

            Func<ServiceHost, ServiceEndpoint> messageEndpointBuilder =
                h =>
                {
                    var messageEndpoint = m_Template.AttachMessageEndpoint(h, messageType, m_Id);
                    return messageEndpoint;
                };
            var host = m_HostBuilder();
            m_MessageHostsByVersion.Add(version, host);

            return host.OpenChannel(messagePipe, messageEndpointBuilder);
        }

        /// <summary>
        /// Closes the current channel.
        /// </summary>
        public void CloseChannel()
        {
            lock (m_Lock)
            {
                var versions = m_MessageHostsByVersion.Keys.ToList();
                foreach (var version in versions)
                {
                    CloseChannel(version);
                }
            }

            RaiseOnClosed();
        }

        private void CloseChannel(Version version)
        {
            lock (m_Lock)
            {
                if (m_SendingEndpoints.ContainsKey(version))
                {
                    var sender = m_SendingEndpoints[version];
                    m_SendingEndpoints.Remove(version);

                    // First notify the recipients that we're closing the channel.
                    var knownEndpoints = new List<ProtocolInformation>(sender.KnownEndpoints());
                    foreach (var key in knownEndpoints)
                    {
                        var msg = new EndpointDisconnectMessage(m_Id);
                        try
                        {
                            // Don't bother retrying this many times because we're about to go away. If the other
                            // side isn't there, they won't care we're not there.
                            sender.Send(key, msg, 1);
                        }
                        catch (FailedToSendMessageException)
                        {
                            // For some reason the message didn't arrive. Honestly we don't
                            // care, we're about to quit, not our problem anymore.
                        }
                    }

                    // Then close the channel. We'll do this in a different
                    // loop to give the channels time to process the messages.
                    foreach (var key in knownEndpoints)
                    {
                        sender.CloseChannelTo(key);
                    }
                }

                if (m_MessageHostsByVersion.ContainsKey(version))
                {
                    var host = m_MessageHostsByVersion[version];
                    m_MessageHostsByVersion.Remove(version);
                    host.CloseConnection();
                }

                if (m_MessagePipesByVersion.ContainsKey(version))
                {
                    var pair = m_MessagePipesByVersion[version];
                    m_MessagePipesByVersion.Remove(version);
                    pair.Item1.OnNewMessage -= pair.Item2;
                }

                if (m_DataHostsByVersion.ContainsKey(version))
                {
                    var host = m_DataHostsByVersion[version];
                    m_DataHostsByVersion.Remove(version);
                    host.CloseConnection();
                }

                if (m_DataPipesByVersion.ContainsKey(version))
                {
                    var pair = m_DataPipesByVersion[version];
                    m_DataPipesByVersion.Remove(version);
                    pair.Item1.OnNewData -= pair.Item2;
                }

                m_LocalConnectionPoints.RemoveAll(c => c.Version.Equals(version));
            }
        }

        /// <summary>
        /// Indicates that the remote endpoint has disconnected.
        /// </summary>
        /// <param name="endpoint">The ID number of the endpoint that has disconnected.</param>
        public void EndpointDisconnected(ProtocolInformation endpoint)
        {
            lock (m_Lock)
            {
                var version = endpoint.Version;
                if (m_SendingEndpoints.ContainsKey(version))
                {
                    var sender = m_SendingEndpoints[version];
                    sender.CloseChannelTo(endpoint);
                }
            }
        }

        /// <summary>
        /// Transfers the data to the receiving endpoint.
        /// </summary>
        /// <param name="receivingEndpoint">The connection information for the endpoint that will receive the data stream.</param>
        /// <param name="filePath">The file path to the file that should be transferred.</param>
        /// <param name="token">The cancellation token that is used to cancel the task if necessary.</param>
        /// <param name="scheduler">The scheduler that is used to run the return task with.</param>
        /// <param name="maximumNumberOfRetries">The maximum number of times the endpoint will try to send the message if delivery fails.</param>
        /// <returns>
        /// An task that indicates when the transfer is complete.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="receivingEndpoint"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="filePath"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     Thrown if <paramref name="filePath"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="FailedToSendMessageException">
        ///     Thrown when the channel fails to deliver the message to the remote endpoint.
        /// </exception>
        public Task TransferData(
            ProtocolInformation receivingEndpoint, 
            string filePath, 
            CancellationToken token, 
            TaskScheduler scheduler = null, 
            int maximumNumberOfRetries = CommunicationConstants.DefaultMaximuNumberOfRetriesForMessageSending)
        {
            {
                Lokad.Enforce.Argument(() => receivingEndpoint);
                Lokad.Enforce.Argument(() => filePath);
                Lokad.Enforce.Argument(() => filePath, Lokad.Rules.StringIs.NotEmpty);
            }

            var sender = SenderForEndpoint(receivingEndpoint);
            return Task.Factory.StartNew(
                () =>
                {
                    // Don't catch any exception because the task will store them if we don't catch them.
                    using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        sender.Send(receivingEndpoint, file, maximumNumberOfRetries);
                    }
                },
                token,
                TaskCreationOptions.LongRunning,
                scheduler ?? TaskScheduler.Default);
        }

        private ISendingEndpoint SenderForEndpoint(ProtocolInformation info)
        {
            var protocolVersion = info.Version;
            lock (m_Lock)
            {
                if (!m_SendingEndpoints.ContainsKey(protocolVersion))
                {
                    var newSender = m_SenderBuilder(m_Id, BuildMessageSendingProxy, BuildDataTransferProxy);
                    m_SendingEndpoints.Add(protocolVersion, newSender);
                }

                return m_SendingEndpoints[protocolVersion];
            }
        }

        /// <summary>
        /// Sends the given message to the receiving endpoint.
        /// </summary>
        /// <param name="endpoint">The connection information for the endpoint to which the message should be send.</param>
        /// <param name="message">The message that should be send.</param>
        /// <param name="maximumNumberOfRetries">The maximum number of times the endpoint will try to send the message if delivery fails.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="endpoint"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="message"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="FailedToSendMessageException">
        ///     Thrown when the channel fails to deliver the message to the remote endpoint.
        /// </exception>
        public void Send(ProtocolInformation endpoint, ICommunicationMessage message, int maximumNumberOfRetries)
        {
            {
                Lokad.Enforce.Argument(() => endpoint);
                Lokad.Enforce.Argument(() => message);
            }

            var sender = SenderForEndpoint(endpoint);
            if (sender == null)
            {
                throw new EndpointNotContactableException();
            }

            sender.Send(endpoint, message, maximumNumberOfRetries);
        }

        private IMessageSendingEndpoint BuildMessageSendingProxy(ProtocolInformation info)
        {
            return m_VersionedMessageSenderBuilder(info.Version, info.MessageAddress);
        }

        private IDataTransferingEndpoint BuildDataTransferProxy(ProtocolInformation info)
        {
            return m_VersionedDataSenderBuilder(info.Version, info.DataAddress);
        }

        /// <summary>
        /// An event raised when a new message is received.
        /// </summary>
        public event EventHandler<MessageEventArgs> OnMessageReception;

        private void RaiseOnMessageReception(ICommunicationMessage message)
        {
            var local = OnMessageReception;
            if (local != null)
            {
                local(this, new MessageEventArgs(message));
            }
        }

        /// <summary>
        /// An event raised when a new data stream is received.
        /// </summary>
        public event EventHandler<DataTransferEventArgs> OnDataReception;

        private void RaiseOnDataReception(DataTransferMessage message)
        {
            var local = OnDataReception;
            if (local != null)
            {
                local(this, new DataTransferEventArgs(message));
            }
        }

        /// <summary>
        /// An event raised when the the channel is closed.
        /// </summary>
        public event EventHandler<ChannelClosedEventArgs> OnClosed;

        private void RaiseOnClosed()
        {
            var local = OnClosed;
            if (local != null)
            {
                local(this, new ChannelClosedEventArgs(m_Id));
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or
        /// resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            CloseChannel();
        }
    }
}
