// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Host.Config;

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

        private class SendGridExtensionConfig : IExtensionConfigProvider
        {
            private SendGridConfiguration _sendGridConfig;

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

                context.Config.RegisterBindingExtension(new SendGridAttributeBindingProvider(_sendGridConfig, context.Config.NameResolver));
            }
        }
    }
}
