﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nuclei.Communication
{
    /// <summary>
    /// Defines the interface for objects that provide the user-end for the communication system.
    /// </summary>
    public interface ICommunicationFacade : INotifyOfEndpointStateChange
    {
        /// <summary>
        /// Gets the endpoint ID of the local endpoint.
        /// </summary>
        EndpointId Id
        {
            get;
        }

        IEnumerable<EndpointId> KnownEndpoints();

        EndpointId FromUri(Uri address);
    }
}
