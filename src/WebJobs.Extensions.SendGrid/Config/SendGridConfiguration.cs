// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using Client;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Config;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SendGrid.Helpers.Mail;

namespace Microsoft.Azure.WebJobs.Extensions.SendGrid
{
    /// <summary>
    /// Defines the configuration options for the SendGrid binding.
    /// </summary>
    public class SendGridConfiguration : IExtensionConfigProvider
    {
        internal const string AzureWebJobsSendGridApiKeyName = "AzureWebJobsSendGridApiKey";

        private ConcurrentDictionary<string, ISendGridClient> _sendGridClientCache = new ConcurrentDictionary<string, ISendGridClient>();

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public SendGridConfiguration()
        {
            ClientFactory = new SendGridClientFactory();
        }

        /// <summary>
        /// Gets or sets the SendGrid ApiKey. If not explicitly set, the value will be defaulted
        /// to the value specified via the 'AzureWebJobsSendGridApiKey' app setting or the
        /// 'AzureWebJobsSendGridApiKey' environment variable.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the default "to" address that will be used for messages.
        /// This value can be overridden by job functions.
        /// </summary>
        /// <remarks>
        /// An example of when it would be useful to provide a default value for 'to' 
        /// would be for emailing your own admin account to notify you when particular
        /// jobs are executed. In this case, job functions can specify minimal info in
        /// their bindings, for example just a Subject and Text body.
        /// </remarks>
        public EmailAddress ToAddress { get; set; }

        /// <summary>
        /// Gets or sets the default "from" address that will be used for messages.
        /// This value can be overridden by job functions.
        /// </summary>
        public EmailAddress FromAddress { get; set; }

        internal ISendGridClientFactory ClientFactory { get; set; }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var metadata = new ConfigMetadata();
            context.ApplyConfig(metadata, "sendgrid");
            this.ToAddress = SendGridHelpers.Apply(this.ToAddress, metadata.To);
            this.FromAddress = SendGridHelpers.Apply(this.FromAddress, metadata.From);

            if (string.IsNullOrEmpty(this.ApiKey))
            {
                INameResolver nameResolver = context.Config.NameResolver;
                this.ApiKey = nameResolver.Resolve(AzureWebJobsSendGridApiKeyName);
            }

            context                
                .AddConverter<string, SendGridMessage>(SendGridHelpers.CreateMessage)
                .AddConverter<JObject, SendGridMessage>(SendGridHelpers.CreateMessage)
                .AddBindingRule<SendGridAttribute>()
                .AddValidator(ValidateBinding)
                    .BindToCollector<SendGridMessage>(CreateCollector);
        }

        private IAsyncCollector<SendGridMessage> CreateCollector(SendGridAttribute attr)
        {
            string apiKey = FirstOrDefault(attr.ApiKey, ApiKey);
            ISendGridClient sendGrid = _sendGridClientCache.GetOrAdd(apiKey, a => ClientFactory.Create(a));
            return new SendGridMessageAsyncCollector(this, attr, sendGrid);
        }

        private void ValidateBinding(SendGridAttribute attribute, Type type)
        {
            ValidateBinding(attribute);
        }

        private void ValidateBinding(SendGridAttribute attribute)
        {
            string apiKey = FirstOrDefault(attribute.ApiKey, ApiKey);

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    $"The SendGrid ApiKey must be set either via an '{AzureWebJobsSendGridApiKeyName}' app setting, via an '{AzureWebJobsSendGridApiKeyName}' environment variable, or directly in code via {nameof(SendGridConfiguration)}.{nameof(SendGridConfiguration.ApiKey)} or {nameof(SendGridAttribute)}.{nameof(SendGridAttribute.ApiKey)}.");
            }
        }

        private static string FirstOrDefault(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrEmpty(v));
        }

        // Schema for host.json 
        private class ConfigMetadata
        {
            [JsonProperty("to")]
            public string To { get; set; }

            [JsonProperty("from")]
            public string From { get; set; }
        }
    }
}
