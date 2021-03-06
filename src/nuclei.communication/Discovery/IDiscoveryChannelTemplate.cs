﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace Nuclei.Communication.Discovery
{
    /// <summary>
    /// Defines the channel type for discovery purposes.
    /// </summary>
    internal interface IDiscoveryChannelTemplate : IChannelTemplate
    {
        /// <summary>
        /// Generates a new binding object for the channel.
        /// </summary>
        /// <returns>
        /// The newly generated binding.
        /// </returns>
        Binding GenerateBinding();

        /// <summary>
        /// Attaches a new endpoint to the given host.
        /// </summary>
        /// <param name="host">The host to which the endpoint should be attached.</param>
        /// <param name="implementedContract">The contract implemented by the endpoint.</param>
        /// <param name="localEndpoint">The ID of the local endpoint, to be used in the endpoint metadata.</param>
        /// <param name="allowAutomaticChannelDiscovery">
        /// A flag that indicates whether or not the channel should provide automatic channel discovery.
        /// </param>
        /// <returns>The newly attached endpoint.</returns>
        ServiceEndpoint AttachDiscoveryEntryEndpoint(
            ServiceHost host, 
            Type implementedContract, 
            EndpointId localEndpoint, 
            bool allowAutomaticChannelDiscovery);

        /// <summary>
        /// Attaches a new endpoint to the given host.
        /// </summary>
        /// <param name="host">The host to which the endpoint should be attached.</param>
        /// <param name="implementedContract">The contract implemented by the endpoint.</param>
        /// <param name="version">The version of the discovery endpoint.</param>
        /// <returns>The newly attached endpoint.</returns>
        ServiceEndpoint AttachVersionedDiscoveryEndpoint(ServiceHost host, Type implementedContract, Version version);
    }
}
