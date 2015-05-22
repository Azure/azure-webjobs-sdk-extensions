using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Triggers;
using WebJobs.Extensions.Timers.Triggers;

namespace WebJobs.Extensions.Timers.Config
{
    /// <summary>
    /// Extension configuration provider used to register File System triggers and binders
    /// </summary>
    internal class TimersExtensionConfig : IExtensionConfigProvider
    {
        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();

            // register our trigger binding provider
            TimersTriggerAttributeBindingProvider triggerBindingProvider = new TimersTriggerAttributeBindingProvider();
            extensions.RegisterExtension<ITriggerBindingProvider>(triggerBindingProvider);
        }
    }
}
