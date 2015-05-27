// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace WebJobs.Extensions.Files
{
    /// <summary>
    /// Extension configuration provider used to register File System triggers and binders
    /// </summary>
    internal class FilesExtensionConfig : IExtensionConfigProvider
    {
        private FilesConfiguration _filesConfig;

        /// <summary>
        /// Creates a new <see cref="FilesExtensionConfig"/> instance.
        /// </summary>
        /// <param name="filesConfig">The <see cref="FilesConfiguration"></see> to use./></param>
        public FilesExtensionConfig(FilesConfiguration filesConfig)
        {
            if (filesConfig == null)
            {
                throw new ArgumentNullException("filesConfig");
            }

            _filesConfig = filesConfig;
        }

        /// <summary>
        /// Gets the <see cref="FilesConfiguration"/>
        /// </summary>
        public FilesConfiguration Config
        {
            get
            {
                return _filesConfig;
            }
        }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // get the services we need to construct our binding providers
            IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();

            // register our trigger binding provider
            FileTriggerAttributeBindingProvider triggerBindingProvider = new FileTriggerAttributeBindingProvider(_filesConfig);
            extensions.RegisterExtension<ITriggerBindingProvider>(triggerBindingProvider);
        }
    }
}
