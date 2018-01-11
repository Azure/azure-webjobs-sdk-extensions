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

        private readonly ConnectionFactory connectionFactory;

        // Map of saas names (ie, "Dropbox") to their underlying root folder. 
        private Dictionary<string, IFolderItem> _map = new Dictionary<string, IFolderItem>(StringComparer.OrdinalIgnoreCase);
        private ApiHubLogger _logger;
        private INameResolver _nameResolver;
        
        private int _maxFunctionExecutionRetryCount = DefaultMaxFunctionExecutionRetryCount;

        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <remarks>This an explicite parameterless constructor.</remarks>
        public ApiHubConfiguration()
            : this(null)
        { }

        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <param name="connectionFactory">The factory used to create connections</param>
        public ApiHubConfiguration(ConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory ?? ConnectionFactory.Default;
        }
        
        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>
        /// The logger.
        /// </value>
        public TraceWriter Logger
        {
            get { return _logger == null ? null : _logger.TraceWriter; }
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
            _nameResolver = config.GetService<INameResolver>();

            var bindingProvider = new GenericStreamBindingProvider<ApiHubFileAttribute, ApiHubFile>(BuildFromAttribute, converterManager, context.Trace);
            extensions.RegisterExtension<IBindingProvider>(bindingProvider);

            var triggerBindingProvider = new GenericFileTriggerBindingProvider<ApiHubFileTriggerAttribute, ApiHubFile>(BuildListener, config, bindingProvider, this, context.Trace);
            extensions.RegisterExtension<ITriggerBindingProvider>(triggerBindingProvider);

            extensions.RegisterExtension<IBindingProvider>(new TableBindingProvider(new TableConfigContext(connectionFactory, _nameResolver)));
        }

        private Task<IListener> BuildListener(JobHostConfiguration config, ApiHubFileTriggerAttribute attribute, string functionName, ITriggeredFunctionExecutor executor, TraceWriter trace)
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

            var folder = root.GetFolderReference(folderName);

            var listener = new ApiHubListener(this, config, folder, functionName, executor, trace, attribute);

            return Task.FromResult<IListener>(listener);
        }

        // Attribute has path resolved
        private Task<ApiHubFile> BuildFromAttribute(ApiHubFileAttribute attribute)
        {
            var source = GetFileSource(attribute.ConnectionStringSetting);
            ApiHubFile file = ApiHubFile.New(source, attribute.Path);
            return Task.FromResult(file);
        }

        private IFolderItem GetFileSource(string key)
        {
            if (_map.ContainsKey(key))
            {
                return _map[key];
            }
            else
            {
                // If the key doesn't exist, see if it is available as an environment variable.
                // This might be the case for imperative binding scenarios.
                var connectionString = _nameResolver.Resolve(key);
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    AddConnection(key, connectionString);
                    return _map[key];
                }
                else
                {
                    throw new ArgumentException($"The App setting or Environment variable {key} does not exist.");
                }
            }
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
