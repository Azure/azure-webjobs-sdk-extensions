// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Azure.Devices;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.IoTHub
{
    /// <summary>
    /// Binding provider for IoTHub
    /// </summary>
    public class IoTHubConfigProvider : IExtensionConfigProvider,
        IConverter<byte[], Message>,
        IConverter<string, Message>,
        IConverter<JObject, Message>,
        IConverter<IoTHubAttribute, ServiceClient>
    {
        internal const string IoTHubConnectionStringName = "AzureWebJobsIoTHub";
        internal readonly ConcurrentDictionary<string, ServiceClient> ClientCache = new ConcurrentDictionary<string, ServiceClient>();
        private string _defaultConnectionString;

        /// <summary>
        /// Gets or sets the IoTHub connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <inheritdoc/>
        public void Initialize(ExtensionConfigContext context)
        {
            INameResolver nameResolver = context.Config.GetService<INameResolver>();
            _defaultConnectionString = nameResolver.Resolve(IoTHubConnectionStringName);

            BindingFactory bf = context.Config.BindingFactory;
            IBindingProvider messageProvider = bf.BindToCollector<IoTHubAttribute, Message>(AttributeToMessageConverter);
            IBindingProvider clientProvider = bf.BindToInput<IoTHubAttribute, ServiceClient>(this);

            IConverterManager cm = context.Config.ConverterManager;
            cm.AddConverter<byte[], Message, IoTHubAttribute>(this);
            cm.AddConverter<string, Message, IoTHubAttribute>(this);
            cm.AddConverter<JObject, Message, IoTHubAttribute>(this);

            context.RegisterBindingRules<IoTHubAttribute>(ValidateConnection, messageProvider, clientProvider);
        }

        internal IAsyncCollector<Message> AttributeToMessageConverter(IoTHubAttribute input)
        {
            return new MessageAsyncCollector(Convert(input), input.DeviceId);
        }

        /// <inheritdoc/>
        public Message Convert(byte[] input)
        {
            return new Message(input);
        }

        /// <inheritdoc/>
        public Message Convert(string input)
        {
            return new Message(Encoding.UTF8.GetBytes(input));
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public ServiceClient Convert(IoTHubAttribute input)
        {
            return ClientCache.GetOrAdd(ResolveConnectionString(input.ConnectionString), (cs) => ServiceClient.CreateFromConnectionString(cs));
        }

        internal void ValidateConnection(IoTHubAttribute attribute, Type paramType)
        {
            if (string.IsNullOrEmpty(ResolveConnectionString(attribute.ConnectionString)))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The IoTHub connection string must be set either via a '{0}' app setting, via the IoTHubAttribute.ConnectionString property, " +
                    "or via IoTHubConfigProvider.ConnectionString.",
                    IoTHubConnectionStringName));
            }
        }

        internal string ResolveConnectionString(string attributeConnectionString)
        {
            if (!string.IsNullOrEmpty(attributeConnectionString))
            {
                return attributeConnectionString;
            }
            else if (!string.IsNullOrEmpty(ConnectionString))
            {
                return ConnectionString;
            }
            else
            {
                return _defaultConnectionString;
            }
        }
    }
}
