// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;
using SendGrid;

namespace ExtensionsSample
{
    public static class SendGridSamples
    {
        /// <summary>
        /// Demonstrates declaratively defining all email properties with parameter binding
        /// to message properties.
        /// </summary>
        public static void ProcessOrder_Declarative(
            [QueueTrigger(@"samples-orders")] Order order,
            [SendGrid(
                To = "{CustomerEmail}",
                Subject = "Thanks for your order (#{OrderId})!",
                Text = "{CustomerName}, we've received your order ({OrderId}) and have begun processing it!")]
            out SendGridMessage message)
        {
            // You can set additional message properties here
            message = new SendGridMessage();
            message.Headers.Add("MyHeader", "MyValue");
        }

        /// <summary>
        /// Demonstrates imperatively setting email properties inline in the function.
        /// </summary>
        [Disable]
        public static void ProcessOrder_Imperative(
            [QueueTrigger(@"samples-orders")] Order order,
            [SendGrid] out SendGridMessage message)
        {
            message = new SendGridMessage()
            {
                Subject = string.Format("Thanks for your order (#{0})!", order.OrderId),
                Text = string.Format("{0}, we've received your order ({1}) and have begun processing it!", order.CustomerName, order.OrderId)
            };
            message.AddTo(order.CustomerEmail);
        }

        /// <summary>
        /// Demonstrates the JObject binding (the JObject will be converted into a SendGridMessage)
        /// </summary>
        [Disable]
        public static void ProcessOrder_JObject(
            [QueueTrigger(@"samples-orders")] Order order,
            [SendGrid] out JObject message)
        {
            message = new JObject()
            {
                { "To", order.CustomerEmail },
                { "subject", string.Format("Thanks for your order (#{0})!", order.OrderId) },
                { "Text", string.Format("{0}, we've received your order ({1}) and have begun processing it!", order.CustomerName, order.OrderId) }
            };
        }

        /// <summary>
        /// Demonstrates the IAsyncCollector binding. This works with JObject
        /// or SendGridMessage. Using IAsyncCollector is also a way to conditionally
        /// send messages from a function.
        /// </summary>
        [Disable]
        public static void ProcessOrder_JObjectAsyncCollector(
            [QueueTrigger(@"samples-orders")] Order order,
            [SendGrid] IAsyncCollector<JObject> messages)
        {
            JObject message = new JObject()
            {
                { "To", order.CustomerEmail },
                { "Subject", string.Format("Thanks for your order (#{0})!", order.OrderId) },
                { "Text", string.Format("{0}, we've received your order ({1}) and have begun processing it!", order.CustomerName, order.OrderId) }
            };

            messages.AddAsync(message);
        }
    }
}
