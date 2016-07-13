﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Table;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Defines the configuration options for the ApiHub binding.
    /// </summary>
    public class ApiHubConfiguration : IExtensionConfigProvider, IFileTriggerStrategy<ApiHubFile>
    {
        private const int DefaultMaxFunctionExecutionRetryCount = 5;
        // Map of saas names (ie, "Dropbox") to their underlying root folder. 
        private Dictionary<string, IFolderItem> _map = new Dictionary<string, IFolderItem>(StringComparer.OrdinalIgnoreCase);
        private ApiHubLogger _logger;

        private int _maxFunctionExecutionRetryCount = DefaultMaxFunctionExecutionRetryCount;
        
        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <param name="connectionFactory">The factory used to create connections</param>
        public ApiHubConfiguration(ConnectionFactory connectionFactory = null)
        {
            ConnectionFactory = connectionFactory ?? ConnectionFactory.Default;
        }

        private ConnectionFactory ConnectionFactory { get; set; }

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>
        /// The logger.
        /// </value>
        public TraceWriter Logger
        {
            get
            {
                if (_logger != null)
                {
                    return _logger.TraceWriter;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (value != null)
                {
                    _logger = new ApiHubLogger(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of times to try processing a file before moving it to the poison queue (where
        /// possible).
        /// </summary>
        public int MaxFunctionExecutionRetryCount
        {
            get { return _maxFunctionExecutionRetryCount; }

            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException("value");
                }

                _maxFunctionExecutionRetryCount = value;
            }
        }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var config = context.Config;
            var extensions = config.GetService<IExtensionRegistry>();
            var converterManager = config.GetService<IConverterManager>();
            var nameResolver = context.Config.NameResolver;

            var bindingProvider = new GenericStreamBindingProvider<ApiHubFileAttribute, ApiHubFile>(
                BuildFromAttribute, converterManager, context.Trace);
            extensions.RegisterExtension<IBindingProvider>(bindingProvider);

            var triggerBindingProvider = new GenericFileTriggerBindingProvider<ApiHubFileTriggerAttribute, ApiHubFile>(
                BuildListener, config, bindingProvider, this, context.Trace);
            extensions.RegisterExtension<ITriggerBindingProvider>(triggerBindingProvider);

            extensions.RegisterExtension<IBindingProvider>(
                new TableBindingProvider(
                    new TableConfigContext(
                        ConnectionFactory, 
                        nameResolver)));
        }

        private async Task<IListener> BuildListener(JobHostConfiguration config, ApiHubFileTriggerAttribute attribute, string functionName, ITriggeredFunctionExecutor executor, TraceWriter trace)
        {
            var root = GetFileSource(attribute.ConnectionStringSetting);

            string path = attribute.Path;
            int i = path.LastIndexOf('/');
            if (i == -1)
            {
                i = path.LastIndexOf('\\');
            }

            string folderName;
            if (i <= 0)
            {
                // This is the root folder
                folderName = "/";
            }
            else
            {
                folderName = path.Substring(0, i);
            }

            var folder = await root.GetFolderReferenceAsync(folderName);

            var listener = new ApiHubListener(this, config, folder, functionName, executor, trace, attribute);

            return listener;
        }

        // Attribute has path resolved
        private async Task<ApiHubFile> BuildFromAttribute(ApiHubFileAttribute attribute)
        {
            var source = GetFileSource(attribute.ConnectionStringSetting);
            ApiHubFile file = await ApiHubFile.New(source, attribute.Path);
            return file;
        }

        private IFolderItem GetFileSource(string key)
        {
            return _map[key];
        }

        /// <summary>
        /// Adds a connection to the configuration
        /// </summary>
        /// <param name="settingName">App settings settingName name that have the connections string</param>
        /// <param name="connectionString">Connection string to access SAAS via ApiHub. <seealso cref="ApiHubJobHostConfigurationExtensions.GetApiHubProviderConnectionStringAsync" /></param>
        public void AddConnection(string settingName, string connectionString)
        {
            var root = ItemFactory.Parse(connectionString, _logger);
            _map[settingName] = root;
        }

        string IFileTriggerStrategy<ApiHubFile>.GetPath(ApiHubFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException("file");
            }

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
