// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Files.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for File System integration
    /// </summary>
    public static class FilesJobHostConfigurationExtensions
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

            config.RegisterExtensionConfigProvider(new FilesExtensionConfig(filesConfig));
        }

        private class FilesExtensionConfig : IExtensionConfigProvider
        {
            private FilesConfiguration _filesConfig;

            public FilesExtensionConfig(FilesConfiguration filesConfig)
            {
                _filesConfig = filesConfig;
            }

            public void Initialize(ExtensionConfigContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                context.Config.RegisterBindingExtensions(
                    new FileTriggerAttributeBindingProvider(_filesConfig, context.Trace),
                    new FileAttributeBindingProvider(_filesConfig));
            }
        }
    }
}
