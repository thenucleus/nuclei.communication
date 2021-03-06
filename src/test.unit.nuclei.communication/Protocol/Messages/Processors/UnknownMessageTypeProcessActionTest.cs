﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using Nuclei.Diagnostics;
using NUnit.Framework;

namespace Nuclei.Communication.Protocol.Messages.Processors
{
    [TestFixture]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented",
        Justification = "Unit tests do not need documentation.")]
    public sealed class UnknownMessageTypeProcessActionTest
    {
        [Test]
        public void MessageTypeToProcess()
        {
            var endpoint = new EndpointId("id");
            SendMessage sendAction = (e, m, r) => { };
            var systemDiagnostics = new SystemDiagnostics((p, s) => { }, null);

            var action = new UnknownMessageTypeProcessAction(endpoint, sendAction, systemDiagnostics);
            Assert.AreEqual(typeof(ICommunicationMessage), action.MessageTypeToProcess);
        }

        [Test]
        public void Invoke()
        {
            var endpoint = new EndpointId("id");

            EndpointId storedEndpoint = null;
            ICommunicationMessage storedMsg = null;
            SendMessage sendAction = 
                (e, m, r) =>
                {
                    storedEndpoint = e;
                    storedMsg = m;
                };

            var systemDiagnostics = new SystemDiagnostics((p, s) => { }, null);

            var action = new UnknownMessageTypeProcessAction(endpoint, sendAction, systemDiagnostics);

            var otherEndpoint = new EndpointId("otherId");
            action.Invoke(new SuccessMessage(otherEndpoint, new MessageId()));

            Assert.AreSame(otherEndpoint, storedEndpoint);
            Assert.IsInstanceOf<FailureMessage>(storedMsg);
        }

        [Test]
        public void InvokeWithFailingResponse()
        {
            var endpoint = new EndpointId("id");

            int count = 0;
            SendMessage sendAction = 
                (e, m, r) =>
                {
                    count++;
                    if (count <= 1)
                    {
                        throw new Exception();
                    }
                };

            int loggerCount = 0;
            var systemDiagnostics = new SystemDiagnostics((p, s) => { loggerCount++; }, null);

            var action = new UnknownMessageTypeProcessAction(endpoint, sendAction, systemDiagnostics);
            action.Invoke(new SuccessMessage(new EndpointId("otherId"), new MessageId()));

            Assert.AreEqual(1, count);
            Assert.AreEqual(1, loggerCount);
        }

        [Test]
        public void InvokeWithFailedChannel()
        {
            var endpoint = new EndpointId("id");
            SendMessage sendAction = (e, m, r) => { throw new Exception(); };

            int count = 0;
            var systemDiagnostics = new SystemDiagnostics((p, s) => { count++; }, null);

            var action = new UnknownMessageTypeProcessAction(endpoint, sendAction, systemDiagnostics);
            action.Invoke(new SuccessMessage(new EndpointId("otherId"), new MessageId()));

            Assert.AreEqual(1, count);
        }
    }
}
