﻿using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Triggers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    /// <summary>
    /// Defines the configuration options for the ApiHub binding.
    /// </summary>
    public class ApiHubConfiguration : IExtensionConfigProvider, IFileTriggerStrategy<ApiHubFile>
    {
        // Map of saas names (ie, "Dropbox") to their underlying root folder. 
        Dictionary<string, IFolderItem> _map = new Dictionary<string, IFolderItem>(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            var config = context.Config;
            var extensions = config.GetService<IExtensionRegistry>();
            var converterManager = config.GetService<IConverterManager>();

            // converterManager.AddConverter<IFileSource, IFileStreamProvider>(src => new Adapter(src));

            var bindingProvider = new GenerericStreamBindingProvider<ApiHubFileAttribute, ApiHubFile>(
                BuildFromAttribute, converterManager);
            extensions.RegisterExtension<IBindingProvider>(bindingProvider);
            // automatic conversion from NativeSaasFile  -->  IFileBinderStrategy2

            var triggerBindingProvider = new GenericFileTriggerBindingProvider<ApiHubFileTriggerAttribute, ApiHubFile>(
                BuildListener, bindingProvider, this);
            extensions.RegisterExtension<ITriggerBindingProvider>(triggerBindingProvider);
        }

        private async Task<IListener> BuildListener(ApiHubFileTriggerAttribute attribute, ITriggeredFunctionExecutor executor)
        {
            var root = GetFileSource(attribute.Key);

            string path = attribute.Path;
            int i = path.LastIndexOf('/');
            if (i == -1)
            {
                throw new InvalidOperationException("Bad path");
            }
            string folderName = path.Substring(0, i);

            var folder = await root.CreateFolderAsync(folderName);

            var listener = new ApiHubListener(folder, executor);

            return listener;
        }

        // Attribute has path resolved
        private Task<ApiHubFile> BuildFromAttribute(ApiHubFileAttribute attribute)
        {
            var source = GetFileSource(attribute.Key);
            ApiHubFile file = new ApiHubFile(source, attribute.Path);

            return Task.FromResult(file);
        }

        private IFolderItem GetFileSource(string key)
        {
            return _map[key];
        }

        /// <summary>
        /// Add path to the configuration
        /// </summary>
        /// <param name="key">App settings key name that have the connections string</param>
        /// <param name="connectionString">Connection string to access SAAS via ApiHub. <seealso cref="ApiHubHelper.GetApiHubProviderConnectionStringAsync"/></param>
        public void AddKeyPath(string key, string connectionString)
        {
            var root = ItemFactory.Parse(connectionString);
            _map[key] = root;
        }

        string IFileTriggerStrategy<ApiHubFile>.GetPath(ApiHubFile file)
        {
            return file.Path;
        }

        void IFileTriggerStrategy<ApiHubFile>.GetStaticBindingContract(IDictionary<string, Type> contract)
        {
            // nop;
        }

        void IFileTriggerStrategy<ApiHubFile>.GetRuntimeBindingContract(ApiHubFile file, IDictionary<string, object> contract)
        {
            // nop;
        }
    }
}
