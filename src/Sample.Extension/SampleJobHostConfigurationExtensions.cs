using System;
using Microsoft.Azure.WebJobs.Host.Config;
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
                    new SampleTriggerAttributeBindingProvider()
                );

                // Register our Table binding extension
                context.Config.RegisterTableBindingExtension(new SampleTableBindingProvider());
            }
        }
    }
}
