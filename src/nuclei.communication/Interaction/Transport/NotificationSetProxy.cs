﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Nuclei.Communication.Interaction.Transport
{
    /// <summary>
    /// Forms the base for remote <see cref="INotificationSet"/> proxy objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This type is not really meant to be used except by the DynamicProxy2 framework, hence
    /// it should be an open type which is not abstract.
    /// </para>
    /// <para>
    /// This type is public because the .NET type loader will throw an exception if it needs to build a dynamic type
    /// based on an internal base class.
    /// </para>
    /// </remarks>
    public class NotificationSetProxy : INotificationSet
    {
        /// <summary>
        /// The lock which is used to guard the event handler collection.
        /// </summary>
        private readonly object m_Lock = new object();

        /// <summary>
        /// The collection that holds the delegates for the different events.
        /// </summary>
        private readonly IDictionary<NotificationId, List<Delegate>> m_EventHandlers
            = new SortedList<NotificationId, List<Delegate>>();

        /// <summary>
        /// Adds the given event handler to the collection of handlers for the given event.
        /// </summary>
        /// <param name="eventId">The ID of the event.</param>
        /// <param name="handler">The event handler.</param>
        internal void AddToEvent(NotificationId eventId, Delegate handler)
        {
            lock (m_Lock)
            {
                if (!m_EventHandlers.ContainsKey(eventId))
                {
                    m_EventHandlers.Add(eventId, new List<Delegate>());
                }

                var delegates = m_EventHandlers[eventId];
                if (!delegates.Contains(handler))
                {
                    delegates.Add(handler);
                }
            }
        }

        /// <summary>
        /// Removes the given event handler from the collection of handlers for the given event.
        /// </summary>
        /// <param name="eventId">The ID of the event.</param>
        /// <param name="handler">The event handler.</param>
        internal void RemoveFromEvent(NotificationId eventId, Delegate handler)
        {
            lock (m_Lock)
            {
                if (m_EventHandlers.ContainsKey(eventId))
                {
                    var delegates = m_EventHandlers[eventId];
                    if (delegates.Remove(handler))
                    {
                        if (delegates.Count == 0)
                        {
                            m_EventHandlers.Remove(eventId);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clears all the event handlers.
        /// </summary>
        /// <remarks>
        ///     Note that this method does not indicate to the remote endpoint that there
        ///     are no more subscribers.
        /// </remarks>
        internal void ClearAllEvents()
        {
            lock (m_Lock)
            {
                // Do we need to send the message that we're clearing the handlers?
                // At the moment probably not because we only use this when we lose the
                // connection to an endpoint.
                m_EventHandlers.Clear();
            }
        }

        /// <summary>
        /// Indicates if there are any subscribers to the given event.
        /// </summary>
        /// <param name="eventId">The ID of the event.</param>
        /// <returns>
        ///     <see langword="true" /> if there is at least one subscriber to the event; otherwise, <see langword="false" />.
        /// </returns>
        [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1628:DocumentationTextMustBeginWithACapitalLetter",
            Justification = "Documentation can start with a language keyword")]
        internal bool HasSubscribers(NotificationId eventId)
        {
            lock (m_Lock)
            {
                if (m_EventHandlers.ContainsKey(eventId))
                {
                    return m_EventHandlers[eventId].Count > 0;
                }
            }

            return false;
        }

        /// <summary>
        /// Raises the event given by the <paramref name="eventId"/> parameter.
        /// </summary>
        /// <param name="eventId">The ID of the event that should be raised.</param>
        /// <param name="args">The event arguments with which the event should be raised.</param>
        internal void RaiseEvent(NotificationId eventId, EventArgs args)
        {
            var obj = SelfReference();
            var delegates = new List<Delegate>();
            lock (m_Lock)
            {
                if (m_EventHandlers.ContainsKey(eventId))
                {
                    delegates.AddRange(m_EventHandlers[eventId]);
                }
            }

            foreach (var del in delegates)
            {
                del.DynamicInvoke(new[] { obj, args });
            }
        }

        /// <summary>
        /// Returns the reference to the 'current' object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is created so that dynamic proxies can override the method and return a 
        /// reference to the proxy. This should prevent the 'leaking' of the this reference to the
        /// outside world. For more information see:
        /// http://kozmic.pl/2009/10/30/castle-dynamic-proxy-tutorial-part-xv-patterns-and-antipatterns
        /// </para>
        /// <para>
        /// Note that this method is <c>protected internal</c> so that the <see cref="CommandSetInterceptorSelector"/>
        /// can get access to the method through an expression tree.
        /// </para>
        /// </remarks>
        /// <returns>
        /// The current object.
        /// </returns>
        protected internal virtual object SelfReference()
        {
            return this;
        }
    }
}
