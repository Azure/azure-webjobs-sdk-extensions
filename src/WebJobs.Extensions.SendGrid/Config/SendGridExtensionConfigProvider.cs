// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using Client;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Config;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using SendGrid.Helpers.Mail;

namespace Microsoft.Azure.WebJobs.Extensions.SendGrid
{
    /// <summary>
    /// Defines the configuration options for the SendGrid binding.
    /// </summary>
    internal class SendGridExtensionConfigProvider : IExtensionConfigProvider
    {
        internal const string AzureWebJobsSendGridApiKeyName = "AzureWebJobsSendGridApiKey";

        private readonly IOptions<SendGridOptions> _options;
        private readonly INameResolver _nameResolver;
        private ConcurrentDictionary<string, ISendGridClient> _sendGridClientCache = new ConcurrentDictionary<string, ISendGridClient>();

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public SendGridExtensionConfigProvider(IOptions<SendGridOptions> options, ISendGridClientFactory clientFactory, INameResolver nameResolver)
        {
            _options = options;
            _nameResolver = nameResolver;
            ClientFactory = clientFactory;
        }

        internal ISendGridClientFactory ClientFactory { get; set; }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (string.IsNullOrEmpty(_options.Value.ApiKey))
            {
                _options.Value.ApiKey = _nameResolver.Resolve(AzureWebJobsSendGridApiKeyName);
            }

            context
                .AddConverter<string, SendGridMessage>(SendGridHelpers.CreateMessage)
                .AddConverter<JObject, SendGridMessage>(SendGridHelpers.CreateMessage);

            var rule = context.AddBindingRule<SendGridAttribute>();
            rule.AddValidator(ValidateBinding);
            rule.BindToCollector<SendGridMessage>(CreateCollector);
        }

        private IAsyncCollector<SendGridMessage> CreateCollector(SendGridAttribute attr)
        {
            string apiKey = FirstOrDefault(attr.ApiKey, _options.Value.ApiKey);
            ISendGridClient sendGrid = _sendGridClientCache.GetOrAdd(apiKey, a => ClientFactory.Create(a));
            return new SendGridMessageAsyncCollector(_options.Value, attr, sendGrid);
        }

        private void ValidateBinding(SendGridAttribute attribute, Type type)
        {
            ValidateBinding(attribute);
        }

        private void ValidateBinding(SendGridAttribute attribute)
        {
            string apiKey = FirstOrDefault(attribute.ApiKey, _options.Value.ApiKey);

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    $"The SendGrid ApiKey must be set either via an '{AzureWebJobsSendGridApiKeyName}' app setting, via an '{AzureWebJobsSendGridApiKeyName}' environment variable, or directly in code via {nameof(SendGridOptions)}.{nameof(SendGridOptions.ApiKey)} or {nameof(SendGridAttribute)}.{nameof(SendGridAttribute.ApiKey)}.");
            }
        }

        private static string FirstOrDefault(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrEmpty(v));
        }
    }
}
