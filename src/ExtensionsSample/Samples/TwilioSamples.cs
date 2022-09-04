// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace ExtensionsSample.Samples
{
    // To use the TwilioSamples:
    // 1. Configure your Twilio Account SID via the 'AzureWebJobsTwilioAccountSid' App Setting or Environment variable
    // 2. Configure your Twilio Auth Token via the 'AzureWebJobsTwilioAuthToken' App Setting or Environment variable
    // 3. Add typeof(TwilioSamples) to the SamplesTypeLocator in Program.cs
    public static class TwilioSamples
    {
        /// <summary>
        /// Demonstrates declaratively SMS message properties with parameter binding
        /// to message properties.
        /// </summary>
        public static void ProcessOrder_Declarative(
            [QueueTrigger(@"samples-orders")] Order order,
            [TwilioSms(
                From = "{StorePhoneNumber}",
                Body = "{CustomerName}, we've received your order ({OrderId}) and have begun processing it!")]
            out CreateMessageOptions messageOptions)
        {
            // You can set additional message properties here
            messageOptions = new CreateMessageOptions(new PhoneNumber(order.CustomerPhoneNumber));
        }

        /// <summary>
        /// Demonstrates imperatively setting SMS message properties inline in the function.
        /// </summary>
        [Disable]
        public static void ProcessOrder_Imperative(
            [QueueTrigger(@"samples-orders")] Order order,
            [TwilioSms] out CreateMessageOptions messageOptions)
        {
            messageOptions = new CreateMessageOptions(new PhoneNumber(order.StorePhoneNumber))
            {
                From = new PhoneNumber(order.StorePhoneNumber),
                Body = string.Format("{0}, we've received your order ({1}) and have begun processing it!", order.CustomerName, order.OrderId)
            };
        }

        /// <summary>
        /// Demonstrates the JObject binding (the JObject will be converted into an message)
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
        /// Demonstrates the IAsyncCollector binding. This works with <see cref="JObject"/>
        /// or <see cref="CreateMessageOptions"/>. Using IAsyncCollector is also a way to conditionally
        /// send messages from a function.
        /// </summary>
        [Disable]
        public static void ProcessOrder_MessageAsyncCollector(
            [QueueTrigger(@"samples-orders")] Order order,
            [TwilioSms] IAsyncCollector<CreateMessageOptions> messages)
        {
            var messageOptions = new CreateMessageOptions(new PhoneNumber(order.CustomerPhoneNumber))
            {
                From = new PhoneNumber(order.StorePhoneNumber),
                Body = string.Format("{0}, we've received your order ({1}) and have begun processing it!", order.CustomerName, order.OrderId)
            };

            messages.AddAsync(messageOptions);
        }

        /// <summary>
        /// Demonstrates the IAsyncCollector binding. This works with <see cref="JObject"/>
        /// or <see cref="CreateMessageOptions"/>. Using IAsyncCollector is also a way to conditionally
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
