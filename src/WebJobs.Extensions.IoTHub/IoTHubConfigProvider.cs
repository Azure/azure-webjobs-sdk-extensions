// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Azure.Devices;
using Microsoft.Azure.WebJobs.Extensions.IoTHub.Converters;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.IoTHub
{
    /// <summary>
    /// Binding provider for IoTHub
    /// </summary>
    public class IoTHubConfigProvider : IExtensionConfigProvider,
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
            IBindingProvider clientProvider = bf.BindToInput(this);

            IConverterManager cm = context.Config.ConverterManager;
            cm.AddConverter<byte[], Message, IoTHubAttribute>(typeof(ByteArrayToMessage));
            cm.AddConverter<string, Message, IoTHubAttribute>(typeof(StringToMessage));
            cm.AddConverter<JObject, Message, IoTHubAttribute>(typeof(JObjectToMessage));

            context.RegisterBindingRules<IoTHubAttribute>(ValidateConnection, messageProvider, clientProvider);
        }

        internal IAsyncCollector<Message> AttributeToMessageConverter(IoTHubAttribute input)
        {
            return new MessageAsyncCollector(Convert(input), input.DeviceId);
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
