// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for SendGrid integration
    /// </summary>
    public static class SendGridJobHostConfigurationExtensions
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

            config.RegisterExtensionConfigProvider(sendGridConfig);
        }
    }
}
