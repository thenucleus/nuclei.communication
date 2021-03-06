﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nuclei.Communication.Protocol
{
    /// <summary>
    /// Defines the interface for objects that handle communication with a remote application.
    /// </summary>
    internal interface IProtocolChannel
    {
        /// <summary>
        /// Returns a collection containing the connection information for each of the available channels.
        /// </summary>
        /// <returns>The collection that contains the connection information for each of the available channels.</returns>
        IEnumerable<ProtocolInformation> LocalConnectionPoints();

        /// <summary>
        /// Returns the connection information for the channel that handles messages for the given version
        /// of the protocol.
        /// </summary>
        /// <param name="protocolVersion">The version of the protocol for which the protocol information should be returned.</param>
        /// <returns>The connection information for the channel that handles messages for the given version of the protocol.</returns>
        ProtocolInformation LocalConnectionPointForVersion(Version protocolVersion);

        /// <summary>
        /// Opens the channel and provides information on how to connect to the given channel.
        /// </summary>
        void OpenChannel();

        /// <summary>
        /// Closes the current channel.
        /// </summary>
        void CloseChannel();

        /// <summary>
        /// Indicates that the remote endpoint has disconnected.
        /// </summary>
        /// <param name="endpoint">The ID number of the endpoint that has disconnected.</param>
        void EndpointDisconnected(ProtocolInformation endpoint);

        /// <summary>
        /// Transfers the data to the receiving endpoint.
        /// </summary>
        /// <param name="receivingEndpoint">The connection information for the endpoint that will receive the data stream.</param>
        /// <param name="filePath">The file path to the file that should be transferred.</param>
        /// <param name="token">The cancellation token that is used to cancel the task if necessary.</param>
        /// <param name="scheduler">The scheduler that is used to run the return task with.</param>
        /// <param name="maximumNumberOfRetries">The maximum number of times the endpoint will try to send the message if delivery fails.</param>
        /// <returns>
        /// An task that indicates when the transfer is complete.
        /// </returns>
        Task TransferData(
            ProtocolInformation receivingEndpoint, 
            string filePath, 
            CancellationToken token, 
            TaskScheduler scheduler = null,
            int maximumNumberOfRetries = CommunicationConstants.DefaultMaximuNumberOfRetriesForMessageSending);

        /// <summary>
        /// Sends the given message to the receiving endpoint.
        /// </summary>
        /// <param name="endpoint">The connection information for the endpoint to which the message should be send.</param>
        /// <param name="message">The message that should be send.</param>
        /// <param name="maximumNumberOfRetries">The maximum number of times the endpoint will try to send the message if delivery fails.</param>
        void Send(ProtocolInformation endpoint, ICommunicationMessage message, int maximumNumberOfRetries);

        /// <summary>
        /// An event raised when a new message is received.
        /// </summary>
        event EventHandler<MessageEventArgs> OnMessageReception;

        /// <summary>
        /// An event raised when a new data stream is received.
        /// </summary>
        event EventHandler<DataTransferEventArgs> OnDataReception;

        /// <summary>
        /// An event raised when the the channel is closed.
        /// </summary>
        event EventHandler<ChannelClosedEventArgs> OnClosed;
    }
}
