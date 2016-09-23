// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;
using SendGrid.Helpers.Mail;

namespace ExtensionsSample
{
    // To use the SendGridSamples:
    // 1. Configure your SendGrid API Key via the 'AzureWebJobsSendGridApiKey' App Setting or Environment variable
    // 4. Add typeof(SendGridSamples) to the SamplesTypeLocator in Program.cs
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
            out Mail message)
        {
            // You can set additional message properties here
            message = new Mail();
            message.AddHeader("MyHeader", "MyValue");
        }

        /// <summary>
        /// Demonstrates imperatively setting email properties inline in the function.
        /// </summary>
        [Disable]
        public static void ProcessOrder_Imperative(
            [QueueTrigger(@"samples-orders")] Order order,
            [SendGrid] out Mail message)
        {
            message = new Mail();
            message.Subject = $"Thanks for your order (#{order.OrderId})!";
            message.AddContent(new Content("text/plain", $"{order.CustomerName}, we've received your order ({order.OrderId}) and have begun processing it!"));

            Personalization personalization = new Personalization();
            personalization.AddTo(new Email(order.CustomerEmail));
            message.AddPersonalization(personalization);
        }

        /// <summary>
        /// Demonstrates the JObject binding.
        /// </summary>  
        [Disable]
        public static void ProcessOrder_JObject(
            [QueueTrigger(@"samples-orders")] Order order,
            [SendGrid(ApiKey = "Test")] out JObject message)
        {
            message = JObject.Parse(GetEmailJson(order));
        }

        /// <summary>
        /// Demonstrates the string binding.
        /// </summary>
        [Disable]
        public static void ProcessOrder_String(
            [QueueTrigger(@"samples-orders")] Order order,
            [SendGrid] out string message)
        {
            message = GetEmailJson(order);
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
            JObject message = JObject.Parse(GetEmailJson(order));
            messages.AddAsync(message);
        }

        private static string GetEmailJson(Order order)
        {
            // Mail reference can be found at: https://sendgrid.com/docs/API_Reference/Web_API_v3/Mail/index.html
            return $@"{{
              'personalizations': [
                {{
                  'to': [
                    {{
                      'email': '{order.CustomerEmail}'
                    }}
                  ]                  
                }}
              ],
              'subject': 'Thanks for your order (#{order.OrderId})!',
              'content': [
                {{
                  'type': 'text/plain',
                  'value': '{order.CustomerName}, we\'ve received your order ({order.OrderId}) and have begun processing it!'
                }}
              ]
            }}";
        }
    }
}
