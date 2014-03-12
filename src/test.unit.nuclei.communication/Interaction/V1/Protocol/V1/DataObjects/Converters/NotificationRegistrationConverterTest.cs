﻿//-----------------------------------------------------------------------
// <copyright company="Nuclei">
//     Copyright 2013 Nuclei. Licensed under the Apache License, Version 2.0.
// </copyright>
//-----------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Nuclei.Communication.Interaction.Transport.Messages;
using Nuclei.Communication.Protocol;
using Nuclei.Communication.Protocol.Messages;
using Nuclei.Communication.Protocol.V1.DataObjects;

namespace Nuclei.Communication.Interaction.V1.Protocol.V1.DataObjects.Converters
{
    [TestFixture]
    [SuppressMessage("Microsoft.StyleCop.CSharp.DocumentationRules", "SA1600:ElementsMustBeDocumented",
        Justification = "Unit tests do not need documentation.")]
    public sealed class NotificationRegistrationConverterTest
    {
        [Test]
        public void MessageTypeToTranslate()
        {
            var translator = new NotificationRegistrationConverter();
            Assert.AreEqual(typeof(RegisterForNotificationMessage), translator.MessageTypeToTranslate);
        }

        [Test]
        public void DataTypeToTranslate()
        {
            var translator = new NotificationRegistrationConverter();
            Assert.AreEqual(typeof(NotificationRegistrationData), translator.DataTypeToTranslate);
        }

        [Test]
        public void ToMessageWithNonMatchingDataType()
        {
            var translator = new NotificationRegistrationConverter();

            var data = new SuccessData
            {
                Id = new MessageId(),
                InResponseTo = new MessageId(),
                Sender = new EndpointId("a"),
            };
            var msg = translator.ToMessage(data);
            Assert.IsInstanceOf(typeof(UnknownMessageTypeMessage), msg);
            Assert.AreSame(data.Id, msg.Id);
            Assert.AreSame(data.Sender, msg.Sender);
            Assert.AreSame(data.InResponseTo, msg.InResponseTo);
        }

        [Test]
        public void ToMessage()
        {
            var translator = new NotificationRegistrationConverter();

            var data = new NotificationRegistrationData
            {
                Id = new MessageId(),
                InResponseTo = new MessageId(),
                Sender = new EndpointId("a"),
                InterfaceType = new SerializedType
                {
                    FullName = typeof(int).FullName,
                    AssemblyName = typeof(int).Assembly.GetName().Name
                },
                EventName = "event",
            };
            var msg = translator.ToMessage(data);
            Assert.IsInstanceOf(typeof(RegisterForNotificationMessage), msg);
            Assert.AreSame(data.Id, msg.Id);
            Assert.AreSame(data.Sender, msg.Sender);
            Assert.AreSame(data.InResponseTo, msg.InResponseTo);
            Assert.AreSame(data.InterfaceType, ((RegisterForNotificationMessage)msg).Notification.InterfaceType.FullName);
            Assert.AreSame(data.EventName, ((RegisterForNotificationMessage)msg).Notification.EventName);
        }

        [Test]
        public void FromMessageWithNonMatchingMessageType()
        {
            var translator = new NotificationRegistrationConverter();

            var msg = new SuccessMessage(new EndpointId("a"), new MessageId());
            var data = translator.FromMessage(msg);
            Assert.IsInstanceOf(typeof(UnknownMessageTypeData), data);
            Assert.AreSame(msg.Id, data.Id);
            Assert.AreSame(msg.Sender, data.Sender);
            Assert.AreSame(msg.InResponseTo, data.InResponseTo);
        }

        [Test]
        public void FromMessage()
        {
            var translator = new NotificationUnregistrationConverter();

            var msg = new RegisterForNotificationMessage(
                new EndpointId("a"),
                new NotificationData(
                    typeof(int),
                    "event"));
            var data = translator.FromMessage(msg);
            Assert.IsInstanceOf(typeof(NotificationRegistrationData), data);
            Assert.AreSame(msg.Id, data.Id);
            Assert.AreSame(msg.Sender, data.Sender);
            Assert.AreSame(msg.InResponseTo, data.InResponseTo);
            Assert.AreSame(msg.Notification.InterfaceType.FullName, ((NotificationRegistrationData)data).InterfaceType.FullName);
            Assert.AreSame(
                msg.Notification.InterfaceType.Assembly.GetName().Name,
                ((NotificationRegistrationData)data).InterfaceType.AssemblyName);
            Assert.AreSame(msg.Notification.EventName, ((NotificationRegistrationData)data).EventName);
        }
    }
}