// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for <see cref="JobHostConfiguration"/> facilitating extensibility scenarios.
    /// </summary>
    public static class JobHostConfigurationExtensions
    {
        /// <summary>
        /// Registers the specified extension with the <see cref="JobHostConfiguration"/>
        /// </summary>
        /// <typeparam name="TExtension">The type of extension being registered.</typeparam>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to register the extension with.</param>
        /// <param name="extension">The extension to register.</param>
        public static void RegisterExtension<TExtension>(this JobHostConfiguration config, TExtension extension)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            extensions.RegisterExtension<TExtension>(extension);
        }

        /// <summary>
        /// Registers the specified <see cref="IExtensionConfigProvider"/>
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to register the extension with.</param>
        /// <param name="extension">The extension to register.</param>
        public static void RegisterExtensionConfigProvider(this JobHostConfiguration config, IExtensionConfigProvider extension)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (extension == null)
            {
                throw new ArgumentNullException("extension");
            }

            config.RegisterExtension(extension);
        }

        /// <summary>
        /// Registers the specified binding extension. The instance must implement one of the supported
        /// binding interfaces (e.g. <see cref="IBindingProvider"/> or <see cref="ITriggerBindingProvider"/>).
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to register the extension with.</param>
        /// <param name="extension">The extension to register.</param>
        public static void RegisterBindingExtension(this JobHostConfiguration config, object extension)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (extension == null)
            {
                throw new ArgumentNullException("extension");
            }

            IBindingProvider bindingProvider = extension as IBindingProvider;
            if (bindingProvider != null)
            {
                config.RegisterExtension<IBindingProvider>(bindingProvider);
                return;
            }

            ITriggerBindingProvider triggerBindingProvider = extension as ITriggerBindingProvider;
            if (triggerBindingProvider != null)
            {
                config.RegisterExtension<ITriggerBindingProvider>(triggerBindingProvider);
                return;
            }

            throw new ArgumentException(string.Format("'{0}' is not a valid binding extension.", extension.GetType()));
        }

        /// <summary>
        /// Registers the specified binding extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to register the extensions with.</param>
        /// <param name="extensions">The extensions to register.</param>
        public static void RegisterBindingExtensions(this JobHostConfiguration config, params object[] extensions)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (extensions == null)
            {
                throw new ArgumentNullException("extensions");
            }

            foreach (object extension in extensions)
            {
                config.RegisterBindingExtension(extension);
            }
        }
    }
}
