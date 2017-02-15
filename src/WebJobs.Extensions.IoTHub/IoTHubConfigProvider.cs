// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Devices;
using Microsoft.Azure.WebJobs.Host.Config;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Extensions.IoTHub
{
    /// <summary>
    /// Binding provider for IoTHub
    /// </summary>
    public class IoTHubConfigProvider : IExtensionConfigProvider,
        IConverter<byte[], Message>,
        IConverter<string, Message>,
        IConverter<JObject, Message>,
        IConverter<IoTHubAttribute, IAsyncCollector<Message>>
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

            var bf = context.Config.BindingFactory;
            var outputProvider = bf.BindToCollector<IoTHubAttribute, Message>(this);

            var cm = context.Config.ConverterManager;
            cm.AddConverter<byte[], Message, IoTHubAttribute>(this);
            cm.AddConverter<string, Message, IoTHubAttribute>(this);
            cm.AddConverter<JObject, Message, IoTHubAttribute>(this);

            context.RegisterBindingRules<IoTHubAttribute>(ValidateConnection, outputProvider);
        }

        /// <inheritdoc/>
        public IAsyncCollector<Message> Convert(IoTHubAttribute input)
        {
            ServiceClient client = GetService(ResolveConnectionString(input.ConnectionString));
            return new MessageAsyncCollector(client, input.DeviceId);
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
            return input.ToObject<Message>();
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
            } else
            {
                return _defaultConnectionString;
            }
        }

        internal ServiceClient GetService(string connectionString)
        {
            return ClientCache.GetOrAdd(connectionString, (cs) => ServiceClient.CreateFromConnectionString(cs));
        }
    }
}
