// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs
{
    public static class OutgoingHttpRequestJobHostConfigurationExtensions
    {
        public static void UseOutgoingHttpRequests(this JobHostConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            // Register our extension configuration provider
            config.RegisterExtensionConfigProvider(new OutgoingHttpRequestExtensionConfig());
        }

        private class OutgoingHttpRequestExtensionConfig : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                // Register our extension binding providers
                context.Config.RegisterBindingExtensions(new OutgoingHttpRequestAttributeBindingProvider());
            }
        }
    }
}
