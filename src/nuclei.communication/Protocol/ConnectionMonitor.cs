﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Nuclei.Configuration;

namespace Nuclei.Communication.Protocol
{
    /// <summary>
    /// Defines methods for monitoring active connections.
    /// </summary>
    internal sealed class ConnectionMonitor : IRegisterConnectionsForMonitoring
    {
        /// <summary>
        /// Stores the connection data for a given connection.
        /// </summary>
        private sealed class ConnectionMap
        {
            /// <summary>
            /// The ID of the endpoint.
            /// </summary>
            private readonly EndpointId m_Endpoint;

            /// <summary>
            /// Initializes a new instance of the <see cref="ConnectionMap"/> class.
            /// </summary>
            /// <param name="endpoint">The ID of the endpoint.</param>
            public ConnectionMap(EndpointId endpoint)
            {
                {
                    Debug.Assert(endpoint != null, "The endpoint should not be a null reference.");
                }

                m_Endpoint = endpoint;
            }

            /// <summary>
            /// Gets the endpoint for the current map.
            /// </summary>
            public EndpointId Endpoint
            {
                get
                {
                    return m_Endpoint;
                }
            }

            /// <summary>
            /// Gets or sets the next point in time that the connection should be tested.
            /// </summary>
            public DateTimeOffset NextConnectionTime
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the number of missed keep-alive signals.
            /// </summary>
            public int NumberOfConnectionFailures
            {
                get;
                set;
            }
        }

        /// <summary>
        /// The collection of endpoints that are being monitored.
        /// </summary>
        private readonly ConcurrentDictionary<EndpointId, ConnectionMap> m_RegisteredConnections
            = new ConcurrentDictionary<EndpointId, ConnectionMap>();

        /// <summary>
        /// The collection of all connection handlers.
        /// </summary>
        private readonly ConcurrentBag<IConfirmConnections> m_ConnectionHandlers
            = new ConcurrentBag<IConfirmConnections>();

        /// <summary>
        /// The collection that contains all the known endpoints.
        /// </summary>
        private readonly IStoreInformationAboutEndpoints m_Endpoints;

        /// <summary>
        /// The function that is used to verify connections to remote endpoints.
        /// </summary>
        private readonly VerifyEndpointConnectionStatusWithCustomData m_VerifyConnectionTo;

        /// <summary>
        /// The function that is used to get the current time and date.
        /// </summary>
        private readonly Func<DateTimeOffset> m_Now;

        /// <summary>
        /// The function that is used to provide the custom keep-alive data.
        /// </summary>
        private readonly KeepAliveCustomDataBuilder m_KeepAliveCustomDataBuilder;

        /// <summary>
        /// The function that is used to return the custom keep-alive data from a response message.
        /// </summary>
        private readonly KeepAliveResponseDataHandler m_KeepAliveResponseDataHandler;

        /// <summary>
        /// The timer which is used to signal the next keep-alive moment.
        /// </summary>
        private readonly ITimer m_Timer;

        /// <summary>
        /// The maximum amount of time that is allowed to expire between two confirmations
        /// of a connection.
        /// </summary>
        private readonly TimeSpan m_MaximumTimeBetweenConnectionConfirmations;

        /// <summary>
        /// The maximum amount of time it may take for a response message to be received.
        /// </summary>
        private readonly TimeSpan m_MessageSendTimeout;

        /// <summary>
        /// The maximum number of keep-alive signals that any connection is allowed to miss
        /// in sequence.
        /// </summary>
        private readonly int m_MaximumNumberOfMissedKeepAliveSignals;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionMonitor"/> class.
        /// </summary>
        /// <param name="endpoints">The collection that contains all the known endpoints.</param>
        /// <param name="verifyConnectionTo">The function that is used to verify connections with remote endpoints.</param>
        /// <param name="timer">The timer which is used to signal the next keep-alive moment.</param>
        /// <param name="now">The function that is used to get the current time and date.</param>
        /// <param name="configuration">The object that stores the configuration for the application.</param>
        /// <param name="keepAliveCustomDataBuilder">The function that is used to provide custom data for the connect message.</param>
        /// <param name="keepAliveResponseDataHandler">The function that is used to return the custom data from a response connect message.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="endpoints"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="verifyConnectionTo"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="timer"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="now"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="configuration"/> is <see langword="null" />.
        /// </exception>
        public ConnectionMonitor(
            IStoreInformationAboutEndpoints endpoints,
            VerifyEndpointConnectionStatusWithCustomData verifyConnectionTo,
            ITimer timer,
            Func<DateTimeOffset> now,
            IConfiguration configuration,
            KeepAliveCustomDataBuilder keepAliveCustomDataBuilder = null,
            KeepAliveResponseDataHandler keepAliveResponseDataHandler = null)
        {
            {
                Lokad.Enforce.Argument(() => endpoints);
                Lokad.Enforce.Argument(() => verifyConnectionTo);
                Lokad.Enforce.Argument(() => timer);
                Lokad.Enforce.Argument(() => now);
                Lokad.Enforce.Argument(() => configuration);
            }

            m_VerifyConnectionTo = verifyConnectionTo;
            m_Now = now;
            m_KeepAliveCustomDataBuilder = keepAliveCustomDataBuilder;
            m_KeepAliveResponseDataHandler = keepAliveResponseDataHandler;

            m_MaximumNumberOfMissedKeepAliveSignals = configuration.HasValueFor(
                    CommunicationConfigurationKeys.MaximumNumberOfMissedConnectionConfirmations)
                ? configuration.Value<int>(CommunicationConfigurationKeys.MaximumNumberOfMissedConnectionConfirmations)
                : CommunicationConstants.DefaultMaximumNumberOfMissedKeepAliveSignals;

            m_MaximumTimeBetweenConnectionConfirmations = configuration.HasValueFor(
                    CommunicationConfigurationKeys.MaximumTimeInMillisecondsBetweenConnectionConfirmations)
                ? TimeSpan.FromMilliseconds(
                    configuration.Value<int>(CommunicationConfigurationKeys.MaximumTimeInMillisecondsBetweenConnectionConfirmations))
                : TimeSpan.FromMilliseconds(CommunicationConstants.DefaultMaximumTimeInMillisecondsBetweenConnectionConfirmations);

            m_MessageSendTimeout = configuration.HasValueFor(CommunicationConfigurationKeys.WaitForResponseTimeoutInMilliSeconds)
                ? TimeSpan.FromMilliseconds(configuration.Value<int>(CommunicationConfigurationKeys.WaitForResponseTimeoutInMilliSeconds))
                : TimeSpan.FromMilliseconds(CommunicationConstants.DefaultWaitForResponseTimeoutInMilliSeconds);

            m_Endpoints = endpoints;
            m_Endpoints.OnEndpointConnected += HandleOnEndpointConnected;
            m_Endpoints.OnEndpointDisconnected += HandleOnEndpointDisconnected;

            m_Timer = timer;
            m_Timer.OnElapsed += HandleKeepAliveIntervalOnElapsed;
        }

        private void HandleOnEndpointConnected(object sender, EndpointEventArgs e)
        {
            var map = new ConnectionMap(e.Endpoint)
                {
                    NextConnectionTime = m_Now() + m_MaximumTimeBetweenConnectionConfirmations,
                    NumberOfConnectionFailures = 0,
                };
            m_RegisteredConnections.TryAdd(e.Endpoint, map);
        }

        private void HandleOnEndpointDisconnected(object sender, EndpointEventArgs e)
        {
            ConnectionMap map;
            m_RegisteredConnections.TryRemove(e.Endpoint, out map);
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "We don't really want the channel to die just because the other side didn't behave properly.")]
        private void HandleKeepAliveIntervalOnElapsed(object sender, EventArgs e)
        {
            var now = m_Now();

            var maps = m_RegisteredConnections
                .Where(p => p.Value.NextConnectionTime < now)
                .Select(p => p.Value)
                .ToList();

            object customData = null;
            if (m_KeepAliveCustomDataBuilder != null)
            {
                customData = m_KeepAliveCustomDataBuilder;
            }

            foreach (var map in maps)
            {
                var endpoint = map.Endpoint;
                try
                {
                    var response = m_VerifyConnectionTo(endpoint, m_MessageSendTimeout, customData);
                    response.ContinueWith(
                        t =>
                        {
                            ConnectionMap localMap;
                            if (!m_RegisteredConnections.TryGetValue(endpoint, out localMap))
                            {
                                return;
                            }

                            if ((t.Exception != null) || t.IsCanceled || t.IsFaulted)
                            {
                                // Timeout
                                // Comms failure
                                int count = ++localMap.NumberOfConnectionFailures;
                                if (count > m_MaximumNumberOfMissedKeepAliveSignals)
                                {
                                    m_Endpoints.TryRemoveEndpoint(endpoint);
                                }

                                return;
                            }

                            localMap.NumberOfConnectionFailures = 0;
                            localMap.NextConnectionTime = m_Now() + m_MaximumTimeBetweenConnectionConfirmations;

                            if (m_KeepAliveResponseDataHandler != null)
                            {
                                m_KeepAliveResponseDataHandler(t.Result);
                            }
                        },
                        TaskContinuationOptions.ExecuteSynchronously);
                }
                catch (Exception)
                {
                    map.NumberOfConnectionFailures += 1;
                }

                if (map.NumberOfConnectionFailures > m_MaximumNumberOfMissedKeepAliveSignals)
                {
                    m_Endpoints.TryRemoveEndpoint(endpoint);
                }
            }
        }

        /// <summary>
        /// Registers a new connection for monitoring.
        /// </summary>
        /// <param name="connectionHandler">The object that handles the connections.</param>
        public void Register(IConfirmConnections connectionHandler)
        {
            {
                Lokad.Enforce.Argument(() => connectionHandler);
            }

            lock (m_ConnectionHandlers)
            {
                IConfirmConnections handler;
                if (m_ConnectionHandlers.TryPeek(out handler))
                {
                    return;
                }

                connectionHandler.OnConfirmChannelIntegrity += HandleOnConfirmChannelIntegrity;
                m_ConnectionHandlers.Add(connectionHandler);
            }
        }

        private void HandleOnConfirmChannelIntegrity(object sender, EndpointEventArgs e)
        {
            ConnectionMap map;
            if (m_RegisteredConnections.TryGetValue(e.Endpoint, out map))
            {
                map.NextConnectionTime = m_Now() + m_MaximumTimeBetweenConnectionConfirmations;
                map.NumberOfConnectionFailures = 0;
            }
        }

        /// <summary>
        /// Unregisters a connection from monitoring.
        /// </summary>
        /// <param name="connectionHandler">The object that handles the connections.</param>
        public void Unregister(IConfirmConnections connectionHandler)
        {
            {
                Lokad.Enforce.Argument(() => connectionHandler);
            }

            lock (m_ConnectionHandlers)
            {
                IConfirmConnections handler;
                if (m_ConnectionHandlers.TryTake(out handler))
                {
                    return;
                }

                handler.OnConfirmChannelIntegrity -= HandleOnConfirmChannelIntegrity;
            }
        }
    }
}
