﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Nuclei.Communication.Interaction
{
    /// <summary>
    /// Registers a set of notifications with the local notification collection and links the notifications to the given communication subjects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A proper notification set class has the following characteristics:
    /// <list type="bullet">
    ///     <item>
    ///         <description>The interface must derive from <see cref="INotificationSet"/>.</description>
    ///     </item>
    ///     <item>
    ///         <description>The interface must only have events, no properties or methods.</description>
    ///     </item>
    ///     <item>
    ///         <description>Each event be based on <see cref="EventHandler{T}"/> delegate.</description>
    ///     </item>
    ///     <item>
    ///         <description>The event must be based on a closed constructed type.</description>
    ///     </item>
    ///     <item>
    ///         <description>The <see cref="EventArgs"/> of <see cref="EventHandler{T}"/> must be serializable.</description>
    ///     </item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="map">The object that defines the mapping of the notification events.</param>
    /// <param name="subject">The communication subjects to which the current notifications belongs.</param>
    public delegate void RegisterNotification(NotificationMap map, params SubjectGroupIdentifier[] subject);
}
