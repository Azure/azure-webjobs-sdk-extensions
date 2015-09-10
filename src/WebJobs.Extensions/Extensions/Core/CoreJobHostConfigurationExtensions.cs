// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Extensions.Core;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for Core extension integration
    /// </summary>
    public static class CoreJobHostConfigurationExtensions
    {
        /// <summary>
        /// Registers the Core extensions
        /// </summary>
        /// <param name="config"></param>
        public static void UseCore(this JobHostConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            config.RegisterExtensionConfigProvider(new CoreExtensionConfig());
        }

        private class CoreExtensionConfig : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                context.Config.RegisterBindingExtension(new ExecutionContextBindingProvider());
            }
        }
    }
}
