// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;
using SendGrid;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for SendGrid integration
    /// </summary>
    public static class SampleJobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of the SendGrid extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="sendGridConfig">The <see cref="SendGridConfiguration"/> to use.</param>
        public static void UseSendGrid(this JobHostConfiguration config, SendGridConfiguration sendGridConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (sendGridConfig == null)
            {
                sendGridConfig = new SendGridConfiguration();
            }

            config.RegisterExtensionConfigProvider(new SendGridExtensionConfig(sendGridConfig));
        }

        internal class SendGridExtensionConfig : IExtensionConfigProvider
        {
            private SendGridConfiguration _sendGridConfig;
            private Web _sendGrid;

            public SendGridExtensionConfig(SendGridConfiguration sendGridConfig)
            {
                _sendGridConfig = sendGridConfig;
            }

            public void Initialize(ExtensionConfigContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                if (string.IsNullOrEmpty(_sendGridConfig.ApiKey))
                {
                    throw new InvalidOperationException(
                        string.Format("The SendGrid ApiKey must be set either via a '{0}' app setting, via a '{0}' environment variable, or directly in code via SendGridConfiguration.ApiKey.",
                        SendGridConfiguration.AzureWebJobsSendGridApiKeyName));
                }
                _sendGrid = new Web(_sendGridConfig.ApiKey);

                IConverterManager converterManager = context.Config.GetService<IConverterManager>();
                converterManager.AddConverter<JObject, SendGridMessage>(SendGridHelpers.CreateMessage);

                INameResolver nameResolver = context.Config.GetService<INameResolver>();
                BindingFactory factory = new BindingFactory(nameResolver, converterManager);
                IBindingProvider outputProvider = factory.BindToAsyncCollector<SendGridAttribute, SendGridMessage>((attr) =>
                {
                    return new SendGridMessageAsyncCollector(_sendGridConfig, attr, _sendGrid);
                });

                IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();
                extensions.RegisterBindingRules<SendGridAttribute>(outputProvider);
            }
        }
    }
}
