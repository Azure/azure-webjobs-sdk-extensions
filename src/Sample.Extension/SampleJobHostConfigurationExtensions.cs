using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Sample.Extension;

namespace Microsoft.Azure.WebJobs
{
    public static class SampleJobHostConfigurationExtensions
    {
        public static void UseSample(this JobHostConfiguration config, SampleConfiguration sampleConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            SampleExtensionConfig extensionConfig = new SampleExtensionConfig(sampleConfig);

            // Register our extension configuration provider
            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<IExtensionConfigProvider>(extensionConfig);
        }

        private class SampleExtensionConfig : IExtensionConfigProvider
        {
            private SampleConfiguration _config;

            public SampleExtensionConfig(SampleConfiguration config)
            {
                _config = config;
            }

            public void Initialize(ExtensionConfigContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();

                // Register our extension binding providers
                SampleAttributeBindingProvider bindingProvider = new SampleAttributeBindingProvider(_config);
                extensions.RegisterExtension<IBindingProvider>(bindingProvider);
            }
        }
    }
}
