﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Nuclei.Communication.Protocol
{
    /// <summary>
    /// Defines the interface for objects that handle incoming connections.
    /// </summary>
    internal interface IConfirmConnections
    {
        /// <summary>
        /// An event raised when data is received from a remote endpoint.
        /// </summary>
        event EventHandler<EndpointEventArgs> OnConfirmChannelIntegrity;
    }
}
