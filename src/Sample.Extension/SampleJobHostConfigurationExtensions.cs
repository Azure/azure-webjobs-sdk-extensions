// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.WindowsAzure.Storage.Table;
using Sample.Extension;

namespace Microsoft.Azure.WebJobs
{
    public static class SampleJobHostConfigurationExtensions
    {
        public static void UseSample(this JobHostConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            // Register our extension configuration provider
            config.RegisterExtensionConfigProvider(new SampleExtensionConfig());
        }

        private class SampleExtensionConfig : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                // Register our extension binding providers
                context.Config.RegisterBindingExtensions(
                    new SampleAttributeBindingProvider(),
                    new SampleTriggerAttributeBindingProvider());

                IConverterManager converterManager = context.Config.GetService<IConverterManager>();
                converterManager.AddConverter<CloudTable, Table<OpenType>, TableAttribute>(typeof(CustomTableBindingConverter<>));
            }
        }

        private class CustomTableBindingConverter<T>
             : IConverter<CloudTable, Table<T>>
        {
            public Table<T> Convert(CloudTable input)
            {
                return new Table<T>(input);
            }
        }
    }
}
