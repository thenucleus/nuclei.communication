﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Nuclei.Communication.Protocol.Messages
{
    /// <summary>
    /// Defines a message that indicates that a certain action has succeeded.
    /// </summary>
    internal sealed class SuccessMessage : CommunicationMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SuccessMessage"/> class.
        /// </summary>
        /// <param name="endpoint">The endpoint which send the current message.</param>
        /// <param name="inResponseTo">The ID number of the message to which the current message is a response.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="endpoint"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="inResponseTo"/> is <see langword="null" />.
        /// </exception>
        public SuccessMessage(EndpointId endpoint, MessageId inResponseTo)
            : this(endpoint, new MessageId(), inResponseTo)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SuccessMessage"/> class.
        /// </summary>
        /// <param name="endpoint">The endpoint which send the current message.</param>
        /// <param name="id">The ID of the current message.</param>
        /// <param name="inResponseTo">The ID number of the message to which the current message is a response.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="endpoint"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="id"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="inResponseTo"/> is <see langword="null" />.
        /// </exception>
        public SuccessMessage(EndpointId endpoint, MessageId id, MessageId inResponseTo)
            : base(endpoint, id, inResponseTo)
        {
        }
    }
}
