// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;

namespace WebJobs.Extensions.Files
{
    /// <summary>
    /// Extension methods for File System integration
    /// </summary>
    public static class JobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of File System extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        public static void UseFiles(this JobHostConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            FilesConfiguration filesConfiguration = new FilesConfiguration();
            // TODO: default the root path to wwwroot

            config.UseFiles(filesConfiguration);
        }

        /// <summary>
        /// Enables use of File System extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="filesConfig">The <see cref="FilesConfiguration"></see> to use./></param>
        public static void UseFiles(this JobHostConfiguration config, FilesConfiguration filesConfig)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (filesConfig == null)
            {
                throw new ArgumentNullException("filesConfig");
            }

            FilesExtensionConfig extensionConfig = new FilesExtensionConfig(filesConfig);

            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<IExtensionConfigProvider>(extensionConfig);
        }
    }
}
