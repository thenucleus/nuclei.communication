﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System.Runtime.Serialization;
using Nuclei.Communication.Protocol.V1;
using Nuclei.Communication.Protocol.V1.DataObjects;

namespace Nuclei.Communication.Interaction.V1.Protocol.V1.DataObjects
{
    /// <summary>
    /// Defines a message that indicates that the sending endpoint has raise an event.
    /// </summary>
    [DataContract]
    internal sealed class NotificationRaisedData : DataObjectBase
    {
        /// <summary>
        /// Gets or sets the ID of the notification which has been raised.
        /// </summary>
        [DataMember]
        public string NotificationId
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the type of the event arguments for the event.
        /// </summary>
        [DataMember]
        public SerializedType EventArgumentsType
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the serialized event arguments for the event.
        /// </summary>
        [DataMember]
        public object EventArguments
        {
            get;
            set;
        }
    }
}
