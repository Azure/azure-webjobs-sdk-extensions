// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;
using Twilio;

namespace ExtensionsSample.Samples
{
    // To use the TwilioSamples:
    // 1. Configure your Twilio Account SID via the 'AzureWebJobsTwilioAccountSid' App Setting or Environment variable
    // 2. Configure your Twilio Auth Token via the 'AzureWebJobsTwilioAuthToken' App Setting or Environment variable
    // 3. Add typeof(TwilioSamples) to the SamplesTypeLocator in Program.cs
    public static class TwilioSamples
    {
        /// <summary>
        /// Demonstrates declaratively defining all SMS message properties with parameter binding
        /// to message properties.
        /// </summary>
        public static void ProcessOrder_Declarative(
            [QueueTrigger(@"samples-orders")] Order order,
            [TwilioSms(
                To = "{CustomerPhoneNumber}",
                From = "{StorePhoneNumber}",
                Body = "{CustomerName}, we've received your order ({OrderId}) and have begun processing it!")]
            out SMSMessage message)
        {
            // You can set additional message properties here
            message = new SMSMessage();
        }

        /// <summary>
        /// Demonstrates imperatively setting SMS message properties inline in the function.
        /// </summary>
        [Disable]
        public static void ProcessOrder_Imperative(
            [QueueTrigger(@"samples-orders")] Order order,
            [TwilioSms] out SMSMessage message)
        {
            message = new SMSMessage()
            {
                From = order.StorePhoneNumber,
                To = order.CustomerPhoneNumber,
                Body = string.Format("{0}, we've received your order ({1}) and have begun processing it!", order.CustomerName, order.OrderId)
            };
        }

        /// <summary>
        /// Demonstrates the JObject binding (the JObject will be converted into an SMSMessage)
        /// </summary>
        [Disable]
        public static void ProcessOrder_JObject(
            [QueueTrigger(@"samples-orders")] Order order,
            [TwilioSms] out JObject message)
        {
            message = new JObject()
            {
                { "From", order.StorePhoneNumber },
                { "To", order.CustomerPhoneNumber },
                { "Body", string.Format("{0}, we've received your order ({1}) and have begun processing it!", order.CustomerName, order.OrderId) }
            };
        }

        /// <summary>
        /// Demonstrates the IAsyncCollector binding. This works with JObject
        /// or SMSMessage. Using IAsyncCollector is also a way to conditionally
        /// send messages from a function.
        /// </summary>
        [Disable]
        public static void ProcessOrder_MessageAsyncCollector(
            [QueueTrigger(@"samples-orders")] Order order,
            [TwilioSms] IAsyncCollector<SMSMessage> messages)
        {
            var message = new SMSMessage()
            {
                From = order.StorePhoneNumber,
                To = order.CustomerPhoneNumber,
                Body = string.Format("{0}, we've received your order ({1}) and have begun processing it!", order.CustomerName, order.OrderId)
            };

            messages.AddAsync(message);
        }

        /// <summary>
        /// Demonstrates the IAsyncCollector binding. This works with JObject
        /// or SMSMessage. Using IAsyncCollector is also a way to conditionally
        /// send messages from a function.
        /// </summary>
        [Disable]
        public static void ProcessOrder_JObjectAsyncCollector(
            [QueueTrigger(@"samples-orders")] Order order,
            [TwilioSms] IAsyncCollector<JObject> messages)
        {
            var message = new JObject()
            {
                { "From", order.StorePhoneNumber },
                { "To", order.CustomerPhoneNumber },
                { "Body", string.Format("{0}, we've received your order ({1}) and have begun processing it!", order.CustomerName, order.OrderId) }
            };

            messages.AddAsync(message);
        }
    }
}
