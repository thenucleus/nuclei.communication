﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;

namespace Nuclei.Communication.Protocol
{
    /// <summary>
    /// Defines the interface for sending messages through a WCF channel.
    /// </summary>
    internal interface ISendingEndpoint
    {
        /// <summary>
        /// Returns the collection of known endpoints.
        /// </summary>
        /// <returns>
        /// The collection of known endpoints.
        /// </returns>
        IEnumerable<ProtocolInformation> KnownEndpoints();

        /// <summary>
        /// Sends the given message.
        /// </summary>
        /// <param name="endpoint">The endpoint to which the message should be send.</param>
        /// <param name="message">The message to be send.</param>
        /// <param name="maximumNumberOfRetries">The maximum number of times the endpoint will try to send the message if delivery fails.</param>
        void Send(ProtocolInformation endpoint, ICommunicationMessage message, int maximumNumberOfRetries);

        /// <summary>
        /// Transfers the stream to the given endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint to which the data should be send.</param>
        /// <param name="data">The data to be send.</param>
        /// <param name="maximumNumberOfRetries">The maximum number of times the endpoint will try to transfer the data if delivery fails.</param>
        void Send(ProtocolInformation endpoint, Stream data, int maximumNumberOfRetries);

        /// <summary>
        /// Closes the channel that connects to the given endpoint.
        /// </summary>
        /// <param name="endpoint">The ID number of the endpoint to which the connection should be closed.</param>
        void CloseChannelTo(ProtocolInformation endpoint);
    }
}
