﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Nuclei.Communication.Protocol;
using Nuclei.Diagnostics;
using Nuclei.Diagnostics.Logging;
using Nuclei.Diagnostics.Profiling;

namespace Nuclei.Communication.Interaction.Transport.Messages.Processors
{
    /// <summary>
    /// Defines the action that processes an <see cref="NotificationRaisedMessage"/>.
    /// </summary>
    internal sealed class NotificationRaisedProcessAction : IMessageProcessAction
    {
        /// <summary>
        /// The object that stores all the notification proxies.
        /// </summary>
        private readonly IRaiseProxyNotifications m_AvailableProxies;

        /// <summary>
        /// The object that provides the diagnostic methods for the system.
        /// </summary>
        private readonly SystemDiagnostics m_Diagnostics;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationRaisedProcessAction"/> class.
        /// </summary>
        /// <param name="availableNotificationProxies">The collection that holds all the registered notification proxies.</param>
        /// <param name="systemDiagnostics">The object that provides the diagnostics method for the system.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="availableNotificationProxies"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="systemDiagnostics"/> is <see langword="null" />.
        /// </exception>
        public NotificationRaisedProcessAction(
            IRaiseProxyNotifications availableNotificationProxies,
            SystemDiagnostics systemDiagnostics)
        {
            {
                Lokad.Enforce.Argument(() => availableNotificationProxies);
                Lokad.Enforce.Argument(() => systemDiagnostics);
            }

            m_AvailableProxies = availableNotificationProxies;
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
                return typeof(NotificationRaisedMessage);
            }
        }

        /// <summary>
        /// Invokes the current action based on the provided message.
        /// </summary>
        /// <param name="message">The message upon which the action acts.</param>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Letting the exception escape will just kill the channel then we won't know what happened, so we log and move on.")]
        public void Invoke(ICommunicationMessage message)
        {
            var msg = message as NotificationRaisedMessage;
            if (msg == null)
            {
                Debug.Assert(false, "The message is of the incorrect type.");
                return;
            }

            var notification = msg.Notification;
            m_Diagnostics.Log(
                LevelToLog.Trace,
                CommunicationConstants.DefaultLogTextPrefix,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Received request to raise event: {0}",
                    notification.Notification));

            try
            {
                using (m_Diagnostics.Profiler.Measure(CommunicationConstants.TimingGroup, "Raise notification"))
                {
                    m_AvailableProxies.RaiseNotification(message.Sender, notification.Notification, notification.EventArgs);
                }
            }
            catch (Exception e)
            {
                m_Diagnostics.Log(
                    LevelToLog.Error,
                    CommunicationConstants.DefaultLogTextPrefix,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Error while raising event {0}. Exception is: {1}",
                        msg.Notification.Notification,
                        e));
            }
        }
    }
}
