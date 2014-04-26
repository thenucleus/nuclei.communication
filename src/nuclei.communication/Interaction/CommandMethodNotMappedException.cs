﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Runtime.Serialization;
using Nuclei.Communication.Properties;

namespace Nuclei.Communication.Interaction
{
    /// <summary>
    /// An exception thrown when one or more of the methods on an <see cref="ICommandSet"/> is not mapped to an instance method.
    /// </summary>
    [Serializable]
    public sealed class CommandMethodNotMappedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandMethodNotMappedException"/> class.
        /// </summary>
        public CommandMethodNotMappedException()
            : this(Resources.Exceptions_Messages_CommandMethodNotMapped)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandMethodNotMappedException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public CommandMethodNotMappedException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandMethodNotMappedException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public CommandMethodNotMappedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandMethodNotMappedException"/> class.
        /// </summary>
        /// <param name="info">
        ///     The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object
        ///     data about the exception being thrown.
        /// </param>
        /// <param name="context">
        ///     The <see cref="T:System.Runtime.Serialization.StreamingContext"/> that contains contextual information
        ///     about the source or destination.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException">
        /// The <paramref name="info"/> parameter is null.
        /// </exception>
        /// <exception cref="T:System.Runtime.Serialization.SerializationException">
        /// The class name is null or <see cref="P:System.Exception.HResult"/> is zero (0).
        /// </exception>
        private CommandMethodNotMappedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
