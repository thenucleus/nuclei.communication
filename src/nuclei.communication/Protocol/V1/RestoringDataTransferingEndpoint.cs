﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Globalization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Nuclei.Communication.Properties;
using Nuclei.Communication.Protocol.V1.DataObjects;
using Nuclei.Diagnostics;
using Nuclei.Diagnostics.Logging;

namespace Nuclei.Communication.Protocol.V1
{
    /// <summary>
    /// Defines the sending end of a WCF data channel which can resurrect the channel if it faults.
    /// </summary>
    /// <source>
    /// Original idea obtained from http://kentb.blogspot.com/2010/01/wcf-channels-faulting-and-dependency.html
    /// </source>
    internal sealed class RestoringDataTransferingEndpoint : IDataTransferingEndpoint
    {
        /// <summary>
        /// The lock object used for getting locks on.
        /// </summary>
        private readonly object m_Lock = new object();

        /// <summary>
        /// The factory which creates new WCF channels.
        /// </summary>
        private readonly ChannelFactory<IDataReceivingEndpointProxy> m_Factory;

        /// <summary>
        /// The object that provides the diagnostics methods for the system.
        /// </summary>
        private readonly SystemDiagnostics m_Diagnostics;

        /// <summary>
        /// The service on the other side of the channel.
        /// </summary>
        private IDataReceivingEndpointProxy m_Service;

        /// <summary>
        /// The channel that handles the connections.
        /// </summary>
        private IChannel m_Channel;

        /// <summary>
        /// A flag that indicates whether the channel has faulted.
        /// </summary>
        private volatile bool m_WasFaulted;

        /// <summary>
        /// Indicates if the current endpoint has been disposed.
        /// </summary>
        private volatile bool m_IsDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestoringDataTransferingEndpoint"/> class.
        /// </summary>
        /// <param name="address">The address of the remote endpoint.</param>
        /// <param name="template">The template that is used to create the binding used to connect to the remote endpoint.</param>
        /// <param name="systemDiagnostics">The object that provides the diagnostic methods for the system.</param>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="address"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="template"/> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown if <paramref name="systemDiagnostics"/> is <see langword="null" />.
        /// </exception>
        public RestoringDataTransferingEndpoint(
            Uri address,
            IProtocolChannelTemplate template,
            SystemDiagnostics systemDiagnostics)
        {
            {
                Lokad.Enforce.Argument(() => address);
                Lokad.Enforce.Argument(() => template);
                Lokad.Enforce.Argument(() => systemDiagnostics);
            }

            var endpoint = new EndpointAddress(address);
            var binding = template.GenerateDataBinding();

            // No need to put a DataContractResolver on this factory because the message type
            // for this endpoint is completely known.
            m_Factory = new ChannelFactory<IDataReceivingEndpointProxy>(binding, endpoint);

            m_Diagnostics = systemDiagnostics;
        }

        /// <summary>
        /// Sends the given message.
        /// </summary>
        /// <param name="message">The message to be send.</param>
        /// <param name="maximumNumberOfRetries">The maximum number of times the endpoint will try to transfer the data if delivery fails.</param>
        public void Send(DataTransferMessage message, int maximumNumberOfRetries)
        {
            var v1Message = TranslateMessage(message);
            SendMessage(v1Message, maximumNumberOfRetries);
        }

        private StreamData TranslateMessage(DataTransferMessage message)
        {
            var result = new StreamData
            {
                SendingEndpoint = message.SendingEndpoint,
                Data = message.Data,
            };

            return result;
        }

        private void SendMessage(StreamData message, int retryCount)
        {
            var initialStreamPosition = message.Data.Position;
            var count = 0;
            Exception exception = null;
            while (count < retryCount)
            {
                EnsureChannelIsAvailable();
                exception = null;

                try
                {
                    var service = m_Service;
                    if (!m_IsDisposed)
                    {
                        var confirmation = service.AcceptStream(message);
                        if ((m_Channel.State == CommunicationState.Opened) && (confirmation != null) && confirmation.WasDataReceived)
                        {
                            return;
                        }
                    }
                }
                catch (FaultException e)
                {
                    m_Diagnostics.Log(
                        LevelToLog.Error,
                        CommunicationConstants.DefaultLogTextPrefix,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Exception occurred during the sending of data. Exception was: {0}",
                            e));

                    // If there is no inner exception then there is no point in keeping the original call stack. 
                    // The originalexception orginates on the other side of the channel which means that there is no
                    // useful stack trace to keep!
                    m_WasFaulted = true;
                    exception = e.InnerException != null
                        ? new FailedToSendMessageException(Resources.Exceptions_Messages_FailedToSendMessage, e.InnerException)
                        : new FailedToSendMessageException();
                }
                catch (CommunicationException e)
                {
                    // Either the connection was aborted or faulted (although it shouldn't be)
                    // or something else nasty went wrong.
                    // 
                    // Abort the channel because it is useless now ..
                    m_WasFaulted = true;
                    exception = new FailedToSendMessageException(Resources.Exceptions_Messages_FailedToSendMessage, e);
                }

                // After the first try the position of the stream will have changed because
                // the WCF connection read from it. In order to retry the message send we'll need
                // to reset it to the original position. If we can't do that, then we just pretend
                // we tried enough times.
                if ((message.Data.Position != initialStreamPosition) && !message.Data.CanSeek)
                {
                    count = retryCount;
                    exception = exception ?? new FailedToSendMessageException();
                }

                message.Data.Position = initialStreamPosition;

                count++;
            }

            if ((m_Channel.State != CommunicationState.Opened) && (exception == null))
            {
                exception = new FailedToSendMessageException();
            }

            if (exception != null)
            {
                throw exception;
            }
        }

        private void EnsureChannelIsAvailable()
        {
            if (ShouldCreateChannel)
            {
                lock (m_Lock)
                {
                    if (ShouldCreateChannel)
                    {
                        if (m_Channel != null)
                        { 
                            // The channel is probably faulted so terminate it.
                            m_Diagnostics.Log(
                                LevelToLog.Info,
                                CommunicationConstants.DefaultLogTextPrefix,
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "Data channel for endpoint at {0} has faulted. Aborting channel.",
                                    m_Factory.Endpoint.Address.Uri));
                            m_Channel.Abort();
                            m_Service.Faulted -= HandleOnChannelFaulting;
                        }

                        m_Diagnostics.Log(
                            LevelToLog.Info,
                            CommunicationConstants.DefaultLogTextPrefix,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Creating data channel for endpoint at {0}.",
                                m_Factory.Endpoint.Address.Uri));

                        m_WasFaulted = false;
                        m_Service = m_Factory.CreateChannel();
                        m_Channel = m_Service;
                        m_Service.Faulted += HandleOnChannelFaulting;
                    }
                }
            }
        }

        private void HandleOnChannelFaulting(object sender, EventArgs e)
        {
            m_WasFaulted = true;
        }

        private bool ShouldCreateChannel
        {
            [DebuggerStepThrough]
            get
            {
                return (!m_IsDisposed) && (m_WasFaulted || (m_Channel == null) || (m_Channel.State != CommunicationState.Opened));
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (m_IsDisposed)
            {
                // We've already disposed of the channel. Job done.
                return;
            }

            m_IsDisposed = true;

            IChannel local;
            lock (m_Lock)
            {
                local = m_Channel;
                m_Channel = null;
            }

            if (local != null && local.State != CommunicationState.Faulted)
            {
                try
                {
                    local.Close();
                    m_Diagnostics.Log(
                        LevelToLog.Debug,
                        CommunicationConstants.DefaultLogTextPrefix,
                        string.Format(
                            CultureInfo.InvariantCulture, 
                            "Disposed of data channel for {0}",
                            m_Factory.Endpoint.Address.Uri));
                }
                catch (CommunicationObjectFaultedException e)
                {
                    // The channel is faulted but there is nothing
                    // we can do about that so just ignore it.
                    m_Diagnostics.Log(
                        LevelToLog.Debug,
                        CommunicationConstants.DefaultLogTextPrefix,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Channel for {0} failed to close normally. Exception was: {1}",
                            m_Factory.Endpoint.Address.Uri,
                            e));
                }
                catch (ProtocolException e)
                {
                    // Apparently the channel was still in use, but we don't want to 
                    // use it anymore so just ignore it
                    m_Diagnostics.Log(
                        LevelToLog.Debug,
                        CommunicationConstants.DefaultLogTextPrefix,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Channel for {0} failed to close normally. Exception was: {1}",
                            m_Factory.Endpoint.Address.Uri,
                            e));
                }
                catch (CommunicationObjectAbortedException e)
                {
                    // The channel is now faulted but there is nothing
                    // we can do about that so just ignore it.
                    m_Diagnostics.Log(
                        LevelToLog.Debug,
                        CommunicationConstants.DefaultLogTextPrefix,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Data channel for {0} failed to close normally. Exception was: {1}",
                            m_Factory.Endpoint.Address.Uri,
                            e));
                }
                catch (CommunicationException e)
                {
                    // Somehow the closing of the channel failed but there is nothing
                    // we can do about that so just ignore it.
                    m_Diagnostics.Log(
                        LevelToLog.Debug,
                        CommunicationConstants.DefaultLogTextPrefix,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Channel for {0} failed to close normally. Exception was: {1}",
                            m_Factory.Endpoint.Address.Uri,
                            e));
                }
                catch (TimeoutException e)
                { 
                    // The default close timeout elapsed before we were 
                    // finished closing the channel. So the channel
                    // is aborted. Nothing we can do, just ignore it.
                    m_Diagnostics.Log(
                        LevelToLog.Debug,
                        CommunicationConstants.DefaultLogTextPrefix,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Data channel for {0} failed to close normally. Exception was: {1}",
                            m_Factory.Endpoint.Address.Uri,
                            e));
                }
            }
        }
    }
}
