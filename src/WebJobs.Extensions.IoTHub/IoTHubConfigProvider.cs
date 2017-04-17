// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Azure.Devices;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.IoTHub
{
    /// <summary>
    /// Binding provider for IoTHub
    /// </summary>
    public class IoTHubConfigProvider : IExtensionConfigProvider, IDisposable
    {
        private readonly ConcurrentDictionary<string, ServiceClient> ClientCache = new ConcurrentDictionary<string, ServiceClient>();
        private readonly static JsonSerializer _serializer = new JsonSerializer();
        private bool _disposed = false;

        /// <inheritdoc/>
        public void Initialize(ExtensionConfigContext context)
        {
            BindingFactory bf = context.Config.BindingFactory;

            context.AddBindingRule<IoTHubAttribute>()
                .AddConverter<byte[], Message>(FromBytes)
                .AddConverter<string, Message>(FromString)
                .AddConverter<JObject, Message>(FromJObject);

            context.AddBindingRule<IoTHubAttribute>()
                .BindToCollector(attr => new MessageAsyncCollector(GetClient(attr), attr.DeviceId));
            context.AddBindingRule<IoTHubAttribute>()
                .BindToInput(attr => GetClient(attr));
        }

        private ServiceClient GetClient(IoTHubAttribute input)
        {
            return ClientCache.GetOrAdd(input.ConnectionString, (cs) => {
                var client = ServiceClient.CreateFromConnectionString(cs);
                client.OpenAsync().GetAwaiter().GetResult();
                return client;
            });
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                var closeTasks = ClientCache.Select((pair) => pair.Value.CloseAsync());
                Task.WhenAll(closeTasks).GetAwaiter().GetResult();
                _disposed = true;
            }
        }

        private Message FromBytes(byte[] bytes) => new Message(bytes);

        private Message FromString(string str) => FromBytes(Encoding.UTF8.GetBytes(str));

        private Message FromJObject(JObject input)
        {
            JToken body = null;
            Message message = null;

            // by convention, use a 'body' property to initialize method
            if (input.TryGetValue("body", StringComparison.OrdinalIgnoreCase, out body))
            {
                message = FromString((string)body);
            }

            _serializer.Populate(input.CreateReader(), message ?? new Message());

            return message;
        }
    }
}
