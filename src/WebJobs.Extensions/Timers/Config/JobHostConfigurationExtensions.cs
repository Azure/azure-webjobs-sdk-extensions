using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using WebJobs.Extensions.Timers.Config;

namespace WebJobs.Extensions.Timers
{
    public static class JobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of the Timer extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        public static void UseTimers(this JobHostConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            TimersExtensionConfig extensionConfig = new TimersExtensionConfig();

            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<IExtensionConfigProvider>(extensionConfig);
        }
    }
}
