﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System.Runtime.Serialization;
using Nuclei.Communication.Protocol.V1.DataObjects;

namespace Nuclei.Communication.Interaction.V1.Protocol.V1.DataObjects
{
    /// <summary>
    /// Defines a message that indicates that the sending endpoint wants to unregister for event notifications.
    /// </summary>
    [DataContract]
    internal sealed class NotificationUnregistrationData : DataObjectBase
    {
        /// <summary>
        /// Gets or sets the ID of the notification from which the endpoint wants to unregister.
        /// </summary>
        [DataMember]
        public string NotificationId
        {
            get;
            set;
        }
    }
}
