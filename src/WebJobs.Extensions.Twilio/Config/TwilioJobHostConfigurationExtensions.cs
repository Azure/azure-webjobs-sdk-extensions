// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Twilio;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for Twilio integration
    /// </summary>
    public static class TwilioJobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of the Twilio extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="twilioSmsConfig">The <see cref="TwilioSmsConfiguration"/> to use.</param>
        public static void UseTwilioSms(this JobHostConfiguration config, TwilioSmsConfiguration twilioSmsConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (twilioSmsConfig == null)
            {
                twilioSmsConfig = new TwilioSmsConfiguration();
            }

            config.RegisterExtensionConfigProvider(twilioSmsConfig);
        }
    }
}
