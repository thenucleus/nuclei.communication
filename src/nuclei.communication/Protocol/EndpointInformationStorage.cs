﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Nuclei.Communication.Protocol
{
    /// <summary>
    /// Stores information about all known endpoints.
    /// </summary>
    internal sealed class EndpointInformationStorage : IStoreEndpointApprovalState
    {
        /// <summary>
        /// The object used to lock on.
        /// </summary>
        private readonly object m_Lock = new object();

        /// <summary>
        /// The collection of endpoints that have been contacted.
        /// </summary>
        private readonly Dictionary<EndpointId, EndpointInformation> m_ContactedEndpoints
            = new Dictionary<EndpointId, EndpointInformation>();

        /// <summary>
        /// The collection of endpoints that have been discovered but have not been approved for connection.
        /// </summary>
        private readonly Dictionary<EndpointId, Tuple<EndpointInformation, ProtocolDescription>> m_EndpointsWaitingForApproval
            = new Dictionary<EndpointId, Tuple<EndpointInformation, ProtocolDescription>>();

        /// <summary>
        /// The collection of endpoints that have been discovered and approved for connection.
        /// </summary>
        private readonly Dictionary<EndpointId, EndpointInformation> m_ApprovedEndpoints
            = new Dictionary<EndpointId, EndpointInformation>();

        /// <summary>
        /// Add a newly discovered endpoint to the collection.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint.</param>
        /// <param name="connection">The connection information for the endpoint.</param>
        /// <returns>
        /// <see langword="true" /> if the endpoint was added; otherwise, <see langword="false" />.
        /// </returns>
        public bool TryAdd(EndpointId endpoint, EndpointInformation connection)
        {
            if ((endpoint != null) && (connection != null))
            {
                lock (m_Lock)
                {
                    if (!m_ContactedEndpoints.ContainsKey(endpoint)
                        && !m_EndpointsWaitingForApproval.ContainsKey(endpoint)
                        && !m_ApprovedEndpoints.ContainsKey(endpoint))
                    {
                        m_ContactedEndpoints.Add(endpoint, connection);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Starts the approval process.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint.</param>
        /// <param name="description">The communication information for the remote endpoint.</param>
        /// <returns>
        /// <see langword="true" /> if the approval process was successfully started; otherwise, <see langword="false" />.
        /// </returns>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1628:DocumentationTextMustBeginWithACapitalLetter",
            Justification = "Documentation can start with a language keyword")]
        public bool TryStartApproval(EndpointId endpoint, ProtocolDescription description)
        {
            if ((endpoint != null) && (description != null))
            {
                lock (m_Lock)
                {
                    if (m_ContactedEndpoints.ContainsKey(endpoint)
                        && !m_EndpointsWaitingForApproval.ContainsKey(endpoint)
                        && !m_ApprovedEndpoints.ContainsKey(endpoint))
                    {
                        var info = m_ContactedEndpoints[endpoint];
                        m_ContactedEndpoints.Remove(endpoint);

                        m_EndpointsWaitingForApproval.Add(
                            endpoint,
                            new Tuple<EndpointInformation, ProtocolDescription>(
                                info,
                                description));
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Completes the approval of the endpoint.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint.</param>
        /// <returns>
        /// <see langword="true" /> if the endpoint was approved; otherwise, <see langword="false" />.
        /// </returns>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1628:DocumentationTextMustBeginWithACapitalLetter",
            Justification = "Documentation can start with a language keyword")]
        public bool TryCompleteApproval(EndpointId endpoint)
        {
            if (endpoint != null)
            {
                EndpointInformation info = null;
                lock (m_Lock)
                {
                    if (!m_ContactedEndpoints.ContainsKey(endpoint)
                        && m_EndpointsWaitingForApproval.ContainsKey(endpoint)
                        && !m_ApprovedEndpoints.ContainsKey(endpoint))
                    {
                        var pair = m_EndpointsWaitingForApproval[endpoint];
                        m_EndpointsWaitingForApproval.Remove(endpoint);
                        m_ApprovedEndpoints.Add(endpoint, pair.Item1);

                        info = pair.Item1;
                    }
                }

                if (info != null)
                {
                    RaiseOnEndpointConnected(info);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Updates the connection information stored for the given endpoint.
        /// </summary>
        /// <param name="connectionInformation">The new connection information.</param>
        /// <returns>
        /// <see langword="true" /> if the endpoint information was updated; otherwise, <see langword="false" />.
        /// </returns>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1628:DocumentationTextMustBeginWithACapitalLetter",
            Justification = "Documentation can start with a language keyword")]
        public bool TryUpdate(EndpointInformation connectionInformation)
        {
            if (connectionInformation != null)
            {
                lock (m_Lock)
                {
                    if (m_ContactedEndpoints.ContainsKey(connectionInformation.Id))
                    {
                        m_ContactedEndpoints[connectionInformation.Id] = connectionInformation;
                        return true;
                    }

                    if (m_EndpointsWaitingForApproval.ContainsKey(connectionInformation.Id))
                    {
                        var tuple = m_EndpointsWaitingForApproval[connectionInformation.Id];
                        m_EndpointsWaitingForApproval.Remove(connectionInformation.Id);
                        m_EndpointsWaitingForApproval.Add(
                            connectionInformation.Id,
                            new Tuple<EndpointInformation, ProtocolDescription>(connectionInformation, tuple.Item2));

                        return true;
                    }
                }
            }

            return false;
        }

        public event EventHandler<EndpointEventArgs> OnEndpointConnected;

        private void RaiseOnEndpointConnected(EndpointInformation info)
        {
            var local = OnEndpointConnected;
            if (local != null)
            {
                local(this, new EndpointEventArgs(info.Id));
            }
        }

        public event EventHandler<EndpointEventArgs> OnEndpointDisconnected;

        private void RaiseOnEndpointDisconnected(EndpointInformation info)
        {
            var local = OnEndpointDisconnected;
            if (local != null)
            {
                local(this, new EndpointEventArgs(info.Id));
            }
        }

        /// <summary>
        /// Indicates if the endpoint has been contacted, but the approval process hasn't been started yet.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint.</param>
        /// <returns>
        /// <see langword="true" /> if the given endpoint has been contacted but has not been approved for communication; 
        /// otherwise, <see langword="false" />.
        /// </returns>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1628:DocumentationTextMustBeginWithACapitalLetter",
            Justification = "Documentation can start with a language keyword")]
        public bool HasBeenContacted(EndpointId endpoint)
        {
            lock (m_Lock)
            {
                return (endpoint != null) && m_ContactedEndpoints.ContainsKey(endpoint);
            }
        }

        /// <summary>
        /// Indicates if the endpoint has been discovered but not approved yet.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint.</param>
        /// <returns>
        /// <see langword="true" /> if the given endpoint has been discovered but has not been approved for communication; 
        /// otherwise, <see langword="false" />.
        /// </returns>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1628:DocumentationTextMustBeginWithACapitalLetter",
            Justification = "Documentation can start with a language keyword")]
        public bool IsWaitingForApproval(EndpointId endpoint)
        {
            lock (m_Lock)
            {
                return (endpoint != null) && m_EndpointsWaitingForApproval.ContainsKey(endpoint);
            }
        }

        /// <summary>
        /// Indicates if it is possible and allowed to communicate with a given endpoint.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint.</param>
        /// <returns>
        /// <see langword="true" /> if it is possible and allowed to communicate with the given endpoint; otherwise, <see langword="false" />.
        /// </returns>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1628:DocumentationTextMustBeginWithACapitalLetter",
            Justification = "Documentation can start with a language keyword")]
        public bool CanCommunicateWithEndpoint(EndpointId endpoint)
        {
            lock (m_Lock)
            {
                return (endpoint != null) && m_ApprovedEndpoints.ContainsKey(endpoint);
            }
        }

        /// <summary>
        /// Returns the connection information for the given endpoint.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint.</param>
        /// <param name="information">The connection information for the endpoint.</param>
        /// <returns>
        /// <see langword="true" /> if the information for the endpoint was retrieved successfully; otherwise, <see langword="false" />.
        /// </returns>
        public bool TryGetConnectionFor(EndpointId endpoint, out EndpointInformation information)
        {
            information = null;
            if (endpoint != null)
            {
                lock (m_Lock)
                {
                    if (m_ApprovedEndpoints.ContainsKey(endpoint))
                    {
                        information = m_ApprovedEndpoints[endpoint];
                        return true;
                    }

                    if (m_EndpointsWaitingForApproval.ContainsKey(endpoint))
                    {
                        information = m_EndpointsWaitingForApproval[endpoint].Item1;
                        return true;
                    }

                    if (m_ContactedEndpoints.ContainsKey(endpoint))
                    {
                        information = m_ContactedEndpoints[endpoint];
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Removes the endpoint from the storage.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint.</param>
        /// <returns>
        /// <see langword="true" /> if the endpoint was removed successfully; otherwise, <see langword="false" />.
        /// </returns>
        public bool TryRemoveEndpoint(EndpointId endpoint)
        {
            if (endpoint == null)
            {
                return false;
            }

            bool hasEndpoint;
            lock (m_Lock)
            {
                hasEndpoint = m_ApprovedEndpoints.ContainsKey(endpoint)
                    || m_EndpointsWaitingForApproval.ContainsKey(endpoint)
                    || m_ContactedEndpoints.ContainsKey(endpoint);
            }

            if (!hasEndpoint)
            {
                return false;
            }

            RaiseOnEndpointDisconnecting(endpoint);

            EndpointInformation info = null;
            lock (m_Lock)
            {
                if (m_ApprovedEndpoints.ContainsKey(endpoint))
                {
                    info = m_ApprovedEndpoints[endpoint];
                    m_ApprovedEndpoints.Remove(endpoint);
                }

                if (m_EndpointsWaitingForApproval.ContainsKey(endpoint))
                {
                    // Always notify because we can send messages to an endpoint
                    // while we're establishing if an endpoint is worth connecting to
                    info = m_EndpointsWaitingForApproval[endpoint].Item1;
                    m_EndpointsWaitingForApproval.Remove(endpoint);
                }

                if (m_ContactedEndpoints.ContainsKey(endpoint))
                {
                    // Always notify because we can send messages to an endpoint
                    // while we're establishing if an endpoint is worth connecting to
                    info = m_ContactedEndpoints[endpoint];
                    m_ContactedEndpoints.Remove(endpoint);
                }
            }

            if (info != null)
            {
                RaiseOnEndpointDisconnected(info);
                return true;
            }

            return false;
        }

        /// <summary>
        /// An event raised when an endpoint is about to sign out. Note that the endpoint may already be
        /// gone from the connection, however the information about the endpoint is still available.
        /// </summary>
        public event EventHandler<EndpointEventArgs> OnEndpointDisconnecting;

        private void RaiseOnEndpointDisconnecting(EndpointId id)
        {
            var local = OnEndpointDisconnecting;
            if (local != null)
            {
                local(this, new EndpointEventArgs(id));
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<EndpointId> GetEnumerator()
        {
            IEnumerable<EndpointId> result;
            lock (m_Lock)
            {
                result = m_ApprovedEndpoints.Keys.ToList();
            }

            foreach (var item in result)
            {
                yield return item;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
