// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.SmtpMail;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for SmtpMail integration
    /// </summary>
    public static class SmtpMailJobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of the SmtpMail extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="smtpMailConfig">The <see cref="SmtpMailConfiguration"/> to use.</param>
        public static void UseSmtpMail(this JobHostConfiguration config, SmtpMailConfiguration smtpMailConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            config.RegisterExtensionConfigProvider(smtpMailConfig ?? new SmtpMailConfiguration());
        }
    }
}
