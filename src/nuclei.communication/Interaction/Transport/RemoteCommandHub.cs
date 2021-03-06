﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Nuclei.Diagnostics;

namespace Nuclei.Communication.Interaction.Transport
{
    /// <summary>
    /// Defines the methods for handling communication commands.
    /// </summary>
    /// <remarks>
    /// Objects can register <see cref="ICommandSet"/> implementations with the <see cref="ICommandCollection"/>. The 
    /// availability and definition of these commands is then passed on to all endpoints that are connected
    /// to the current endpoint. Upon reception of command information an endpoint will generate a proxy for
    /// the command interface thereby allowing remote invocation of commands through the proxy command interface.
    /// </remarks>
    internal sealed class RemoteCommandHub : RemoteEndpointProxyHub<CommandSetProxy>, ISendCommandsToRemoteEndpoints, IStoreRemoteCommandProxies
    {
        /// <summary>
        /// The collection that holds all the <see cref="ICommandSet"/> proxies for each endpoint that
        /// has been registered.
        /// </summary>
        private readonly IDictionary<EndpointId, IDictionary<Type, CommandSetProxy>> m_RemoteCommands
            = new Dictionary<EndpointId, IDictionary<Type, CommandSetProxy>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteCommandHub"/> class.
        /// </summary>
        /// <param name="endpointInformationStorage">The object that provides notification of the signing in and signing out of endpoints.</param>
        /// <param name="builder">The object that is responsible for building the command proxies.</param>
        /// <param name="systemDiagnostics">The object that provides the diagnostics methods for the system.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="endpointInformationStorage"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="builder"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="systemDiagnostics"/> is <see langword="null" />.
        /// </exception>
        internal RemoteCommandHub(
            IStoreInformationAboutEndpoints endpointInformationStorage,
            CommandProxyBuilder builder,
            SystemDiagnostics systemDiagnostics)
            : base(
                endpointInformationStorage,
                (endpoint, type) => (CommandSetProxy)builder.ProxyConnectingTo(endpoint, type),
                systemDiagnostics)
        {
            {
                Lokad.Enforce.Argument(() => builder);
            }
        }

        /// <summary>
        /// Returns the name of the proxy objects for use in the trace logs.
        /// </summary>
        /// <returns>A string containing the name of the proxy objects for use in the trace logs.</returns>
        protected override string TraceNameForProxyObjects()
        {
            return "commands";
        }

        /// <summary>
        /// Returns a value indicating if one or more proxies exist for the given endpoint.
        /// </summary>
        /// <param name="endpoint">The ID number of the endpoint.</param>
        /// <returns>
        ///     <see langword="true" /> if one or more proxies exist for the endpoint; otherwise, <see langword="false" />.
        /// </returns>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1628:DocumentationTextMustBeginWithACapitalLetter",
            Justification = "Documentation can start with a language keyword")]
        protected override bool HasProxyFor(EndpointId endpoint)
        {
            return m_RemoteCommands.ContainsKey(endpoint);
        }

        /// <summary>
        /// Adds the collection of proxies to the storage.
        /// </summary>
        /// <param name="endpoint">The endpoint from which the proxies came.</param>
        /// <param name="list">The collection of proxies.</param>
        protected override void AddProxiesToStorage(EndpointId endpoint, SortedList<Type, CommandSetProxy> list)
        {
            if (!m_RemoteCommands.ContainsKey(endpoint))
            {
                m_RemoteCommands.Add(endpoint, list);
            }
            else
            {
                foreach (var pair in list)
                {
                    var existingList = (SortedList<Type, CommandSetProxy>)m_RemoteCommands[endpoint];
                    if (!existingList.ContainsKey(pair.Key))
                    {
                        existingList.Add(pair.Key, pair.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the proxy to the storage.
        /// </summary>
        /// <param name="endpoint">The endpoint from which the proxies came.</param>
        /// <param name="proxyType">The type of the proxy.</param>
        /// <param name="proxy">The proxy.</param>
        protected override void AddProxyFor(EndpointId endpoint, Type proxyType, CommandSetProxy proxy)
        {
            if (m_RemoteCommands.ContainsKey(endpoint))
            {
                var list = m_RemoteCommands[endpoint];
                if (!list.ContainsKey(proxyType))
                {
                    list.Add(proxyType, proxy);
                }
            }
            else
            {
                var list = new SortedList<Type, CommandSetProxy>(new TypeComparer())
                    {
                        {
                            proxyType, proxy
                        }
                    };
                m_RemoteCommands.Add(endpoint, list);
            }
        }

        /// <summary>
        /// Removes all the proxies for the given endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint for which all the proxies have to be removed.</param>
        protected override void RemoveProxiesFor(EndpointId endpoint)
        {
            m_RemoteCommands.Remove(endpoint);
        }

        /// <summary>
        /// Returns a value indicating if a specific set of commands is available for the given endpoint.
        /// </summary>
        /// <param name="endpoint">The ID number of the endpoint.</param>
        /// <param name="commandInterfaceType">The type of the command that should be available.</param>
        /// <returns>
        ///     <see langword="true" /> if there are the specific commands exist for the given endpoint; otherwise, <see langword="false" />.
        /// </returns>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1628:DocumentationTextMustBeginWithACapitalLetter",
            Justification = "Documentation can start with a language keyword")]
        public bool HasCommandFor(EndpointId endpoint, Type commandInterfaceType)
        {
            lock (Lock)
            {
                if (m_RemoteCommands.ContainsKey(endpoint))
                {
                    var commands = m_RemoteCommands[endpoint];
                    return commands.ContainsKey(commandInterfaceType);
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the command proxy for the given endpoint.
        /// </summary>
        /// <typeparam name="TCommand">The typeof command set that should be returned.</typeparam>
        /// <param name="endpoint">The ID number of the endpoint for which the commands should be returned.</param>
        /// <returns>The requested command set.</returns>
        public TCommand CommandsFor<TCommand>(EndpointId endpoint) where TCommand : class, ICommandSet
        {
            return CommandsFor(endpoint, typeof(TCommand)) as TCommand;
        }

        /// <summary>
        /// Returns the command proxy for the given endpoint.
        /// </summary>
        /// <param name="endpoint">The ID number of the endpoint for which the commands should be returned.</param>
        /// <param name="commandType">The type of the command.</param>
        /// <returns>The requested command set.</returns>
        public ICommandSet CommandsFor(EndpointId endpoint, Type commandType)
        {
            lock (Lock)
            {
                if (!m_RemoteCommands.ContainsKey(endpoint))
                {
                    return null;
                }

                var commandSets = m_RemoteCommands[endpoint];
                if (!commandSets.ContainsKey(commandType))
                {
                    return null;
                }

                var result = commandSets[commandType];
                return result;
            }
        }

        /// <summary>
        /// Handles the reception of new command types.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint that owns the commands.</param>
        /// <param name="commandTypes">An array containing the command types for a given endpoint.</param>
        public void OnReceiptOfEndpointCommands(EndpointId endpoint, IEnumerable<OfflineTypeInformation> commandTypes)
        {
            OnReceiptOfEndpointProxies(endpoint, commandTypes);
        }
    }
}
