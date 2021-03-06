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
    /// Defines the methods for handling communication notifications.
    /// </summary>
    /// <remarks>
    /// Objects can register <see cref="INotificationSet"/> implementations with the <see cref="INotificationCollection"/>. The 
    /// availability and definition of these notifications is then passed on to all endpoints that are connected
    /// to the current endpoint. Upon reception of notification information an endpoint will generate a proxy for
    /// the notification interface thereby allowing remote listening to notification events through the proxy notification interface.
    /// </remarks>
    internal sealed class RemoteNotificationHub 
        : RemoteEndpointProxyHub<NotificationSetProxy>, INotifyOfRemoteEndpointEvents, IStoreRemoteNotificationProxies, IRaiseProxyNotifications
    {
        /// <summary>
        /// The collection that holds all the <see cref="INotificationSet"/> proxies for each endpoint that
        /// has been registered.
        /// </summary>
        private readonly IDictionary<EndpointId, IDictionary<Type, NotificationSetProxy>> m_RemoteNotifications
            = new Dictionary<EndpointId, IDictionary<Type, NotificationSetProxy>>();

        /// <summary>
        /// The collection containing the notification proxies mapped based on the ID of the notifications.
        /// </summary>
        private readonly Dictionary<EndpointId, IDictionary<NotificationId, NotificationSetProxy>> m_ProxiesByNotification
            = new Dictionary<EndpointId, IDictionary<NotificationId, NotificationSetProxy>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteNotificationHub"/> class.
        /// </summary>
        /// <param name="endpointInformationStorage">The object that provides notification of the signing in and signing out of endpoints.</param>
        /// <param name="builder">The object that is responsible for building the command proxies.</param>
        /// <param name="systemDiagnostics">The object that provides the diagnostic methods for the system.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="endpointInformationStorage"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="builder"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="systemDiagnostics"/> is <see langword="null" />.
        /// </exception>
        internal RemoteNotificationHub(
            IStoreInformationAboutEndpoints endpointInformationStorage,
            NotificationProxyBuilder builder,
            SystemDiagnostics systemDiagnostics)
            : base(
                endpointInformationStorage,
                (endpoint, type) => (NotificationSetProxy)builder.ProxyConnectingTo(endpoint, type),
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
            return "notifications";
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
            return m_RemoteNotifications.ContainsKey(endpoint);
        }

        /// <summary>
        /// Adds the collection of proxies to the storage.
        /// </summary>
        /// <param name="endpoint">The endpoint from which the proxies came.</param>
        /// <param name="list">The collection of proxies.</param>
        protected override void AddProxiesToStorage(EndpointId endpoint, SortedList<Type, NotificationSetProxy> list)
        {
            foreach (var pair in list)
            {
                AddProxyFor(endpoint, pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// Adds the proxy to the storage.
        /// </summary>
        /// <param name="endpoint">The endpoint from which the proxies came.</param>
        /// <param name="proxyType">The type of the proxy.</param>
        /// <param name="proxy">The proxy.</param>
        protected override void AddProxyFor(EndpointId endpoint, Type proxyType, NotificationSetProxy proxy)
        {
            if (m_RemoteNotifications.ContainsKey(endpoint))
            {
                var list = m_RemoteNotifications[endpoint];
                if (!list.ContainsKey(proxyType))
                {
                    list.Add(proxyType, proxy);
                }
            }
            else
            {
                var list = new SortedList<Type, NotificationSetProxy>(new TypeComparer())
                    {
                        {
                            proxyType, proxy
                        }
                    };
                m_RemoteNotifications.Add(endpoint, list);
            }

            if (!m_ProxiesByNotification.ContainsKey(endpoint))
            {
                m_ProxiesByNotification.Add(endpoint, new Dictionary<NotificationId, NotificationSetProxy>());
            }

            var collection = m_ProxiesByNotification[endpoint];
            var events = proxyType.GetEvents();
            foreach (var eventInfo in events)
            {
                var id = NotificationId.Create(eventInfo);
                collection.Add(id, proxy);
            }
        }

        /// <summary>
        /// Removes all the proxies for the given endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint for which all the proxies have to be removed.</param>
        protected override void RemoveProxiesFor(EndpointId endpoint)
        {
            if (m_RemoteNotifications.ContainsKey(endpoint))
            {
                var list = m_RemoteNotifications[endpoint];
                foreach (var pair in list)
                {
                    pair.Value.ClearAllEvents();
                }
            }

            m_RemoteNotifications.Remove(endpoint);
            m_RemoteNotifications.Remove(endpoint);
        }

        /// <summary>
        /// Returns a value indicating if a specific set of notifications is available for the given endpoint.
        /// </summary>
        /// <param name="endpoint">The ID number of the endpoint.</param>
        /// <param name="notificationInterfaceType">The type of the notification that should be available.</param>
        /// <returns>
        ///     <see langword="true" /> if there are the specific notifications exist for the given endpoint; otherwise, <see langword="false" />.
        /// </returns>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1628:DocumentationTextMustBeginWithACapitalLetter",
            Justification = "Documentation can start with a language keyword")]
        public bool HasNotificationFor(EndpointId endpoint, Type notificationInterfaceType)
        {
            lock (Lock)
            {
                if (m_RemoteNotifications.ContainsKey(endpoint))
                {
                    var commands = m_RemoteNotifications[endpoint];
                    return commands.ContainsKey(notificationInterfaceType);
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the notification proxy for the given endpoint.
        /// </summary>
        /// <typeparam name="TNotification">The typeof notification set that should be returned.</typeparam>
        /// <param name="endpoint">The ID number of the endpoint for which the notifications should be returned.</param>
        /// <returns>The requested notification set.</returns>
        public TNotification NotificationsFor<TNotification>(EndpointId endpoint) where TNotification : class, INotificationSet
        {
            return NotificationsFor(endpoint, typeof(TNotification)) as TNotification;
        }

        /// <summary>
        /// Returns the notification proxy for the given endpoint.
        /// </summary>
        /// <param name="endpoint">The ID number of the endpoint for which the notification should be returned.</param>
        /// <param name="notificationType">The type of the notification.</param>
        /// <returns>The requested notification set.</returns>
        public INotificationSet NotificationsFor(EndpointId endpoint, Type notificationType)
        {
            lock (Lock)
            {
                if (!m_RemoteNotifications.ContainsKey(endpoint))
                {
                    return null;
                }

                var notificationSets = m_RemoteNotifications[endpoint];
                if (!notificationSets.ContainsKey(notificationType))
                {
                    throw new NotificationNotSupportedException(notificationType);
                }

                var result = notificationSets[notificationType];
                return result;
            }
        }

        /// <summary>
        /// Handles the reception of new notification types.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint that owns the notifications.</param>
        /// <param name="notificationTypes">An array containing the notification types for a given endpoint.</param>
        public void OnReceiptOfEndpointNotifications(EndpointId endpoint, IEnumerable<OfflineTypeInformation> notificationTypes)
        {
            OnReceiptOfEndpointProxies(endpoint, notificationTypes);
        }

        /// <summary>
        /// Raises the notification with the given notification ID.
        /// </summary>
        /// <param name="endpoint">The ID of the endpoint that raised the notification.</param>
        /// <param name="id">The ID of the notification.</param>
        /// <param name="args">The event arguments for the notification.</param>
        public void RaiseNotification(EndpointId endpoint, NotificationId id, EventArgs args)
        {
            if (m_ProxiesByNotification.ContainsKey(endpoint))
            {
                var collection = m_ProxiesByNotification[endpoint];
                if (collection.ContainsKey(id))
                {
                    var proxy = collection[id];
                    proxy.RaiseEvent(id, args);
                }
            }
        }
    }
}
