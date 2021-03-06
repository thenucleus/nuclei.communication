﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nuclei.Communication.Properties;
using Nuclei.Diagnostics;
using Nuclei.Diagnostics.Profiling;

namespace Nuclei.Communication.Protocol.Messages.Processors
{
    /// <summary>
    /// Defines the action that processes an <see cref="EndpointConnectMessage"/>.
    /// </summary>
    internal sealed class EndpointConnectProcessAction : IMessageProcessAction
    {
        /// <summary>
        /// The collection that contains all the known <see cref="IChannelTemplate"/> of the 
        /// communication channel from which the messages that are being processed originate.
        /// </summary>
        private readonly List<ChannelTemplate> m_ConnectedChannelTypes = new List<ChannelTemplate>();

        /// <summary>
        /// The object that handles the handshake protocol.
        /// </summary>
        private readonly IHandleProtocolHandshakes m_HandShakeHandler;

        /// <summary>
        /// The object that provides the diagnostics methods.
        /// </summary>
        private readonly SystemDiagnostics m_Diagnostics;

        /// <summary>
        /// Initializes a new instance of the <see cref="EndpointConnectProcessAction"/> class.
        /// </summary>
        /// <param name="protocolHandshakeHandler">
        /// The object that handles the handshake protocol for the current endpoint.
        /// </param>
        /// <param name="channelTypes">
        /// The collection that contains all possible <see cref="IChannelTemplate"/> types for the 
        /// communication channel from which the messages that are being processed originate.
        /// </param>
        /// <param name="systemDiagnostics">The object that provides the diagnostics methods for the system.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="protocolHandshakeHandler"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="channelTypes"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="InvalidChannelTypeException">
        ///     Thrown if one of the entries in <paramref name="channelTypes"/> is <see cref="ChannelTemplate.None"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="systemDiagnostics"/> is <see langword="null" />.
        /// </exception>
        public EndpointConnectProcessAction(
            IHandleProtocolHandshakes protocolHandshakeHandler,
            IEnumerable<ChannelTemplate> channelTypes,
            SystemDiagnostics systemDiagnostics)
        {
            {
                Lokad.Enforce.Argument(() => protocolHandshakeHandler);

                Lokad.Enforce.Argument(() => channelTypes);
                Lokad.Enforce.With<InvalidChannelTypeException>(
                    channelTypes.All(t => t != ChannelTemplate.None),
                    Resources.Exceptions_Messages_AChannelTypeMustBeDefined);

                Lokad.Enforce.Argument(() => systemDiagnostics);
            }

            m_ConnectedChannelTypes.AddRange(channelTypes);
            m_HandShakeHandler = protocolHandshakeHandler;
            m_Diagnostics = systemDiagnostics;
        }

        /// <summary>
        /// Gets the message type that can be processed by this filter action.
        /// </summary>
        /// <value>The message type to process.</value>
        public Type MessageTypeToProcess
        {
            [DebuggerStepThrough]
            get
            {
                return typeof(EndpointConnectMessage);
            }
        }

        /// <summary>
        /// Invokes the current action based on the provided message.
        /// </summary>
        /// <param name="message">The message upon which the action acts.</param>
        public void Invoke(ICommunicationMessage message)
        {
            var msg = message as EndpointConnectMessage;
            if (msg == null)
            {
                Debug.Assert(false, "The message is of the incorrect type.");
                return;
            }

            using (m_Diagnostics.Profiler.Measure(CommunicationConstants.TimingGroup, "Endpoint trying to connect"))
            {
                m_HandShakeHandler.ContinueHandshakeWith(
                    new EndpointInformation(
                        msg.Sender, 
                        msg.DiscoveryInformation, 
                        msg.ProtocolInformation),
                    msg.Information,
                    msg.Id);
            }
        }
    }
}
