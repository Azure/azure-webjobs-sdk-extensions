// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Devices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.IoTHub.Converters
{
    internal class JObjectToMessage : IConverter<JObject, Message>
    {
        public Message Convert(JObject input)
        {
            JToken body = null;
            var message = input.ToObject<Message>();

            // by convention, use a 'body' property to initialize method
            if (input.TryGetValue("body", StringComparison.OrdinalIgnoreCase, out body))
            {
                // can only set body through constructor
                var messageWithBody = new Message(Encoding.UTF8.GetBytes((string)body));
                messageWithBody.Ack = message.Ack;
                messageWithBody.CorrelationId = message.CorrelationId;
                messageWithBody.ExpiryTimeUtc = message.ExpiryTimeUtc;
                messageWithBody.MessageId = message.MessageId;
                foreach (KeyValuePair<string, string> pair in message.Properties)
                {
                    messageWithBody.Properties.Add(pair);
                }
                messageWithBody.To = message.To;
                messageWithBody.UserId = message.UserId;
                message.Dispose();
                return messageWithBody;
            }
            return message;
        }
    }
}
