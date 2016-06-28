// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Reflection;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Script.Extensibility;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    /// <summary>
    /// BindingProvider for ApiHub extensions
    /// </summary>
    public class ApiHubScriptBindingProvider : ScriptBindingProvider
    {
        private readonly ApiHubConfiguration _apiHubConfig = new ApiHubConfiguration();

        /// <inheritdoc/>
        public ApiHubScriptBindingProvider(JobHostConfiguration config, JObject hostMetadata, TraceWriter traceWriter) 
            : base(config, hostMetadata, traceWriter)
        {
        }

        /// <inheritdoc/>
        public override bool TryCreate(ScriptBindingContext context, out ScriptBinding binding)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            binding = null;

            if (string.Compare(context.Type, "apiHubFileTrigger", StringComparison.OrdinalIgnoreCase) == 0 ||
                string.Compare(context.Type, "apiHubFile", StringComparison.OrdinalIgnoreCase) == 0)
            {
                binding = new ApiHubFileScriptBinding(_apiHubConfig, context);
            }
            else if (string.Compare(context.Type, "apiHubTable", StringComparison.OrdinalIgnoreCase) == 0)
            {
                binding = new ApiHubTableScriptBinding(context);
            }

            return binding != null;
        }

        /// <inheritdoc/>
        public override void Initialize()
        {
            if (_apiHubConfig.Logger == null)
            {
                _apiHubConfig.Logger = TraceWriter;
            }

            Config.UseApiHub(_apiHubConfig);
        }

        /// <inheritdoc/>
        public override bool TryResolveAssembly(string assemblyName, out Assembly assembly)
        {
            assembly = null;

            if (string.Compare(assemblyName, "Microsoft.Azure.ApiHub.Sdk", StringComparison.OrdinalIgnoreCase) == 0)
            {
                assembly = typeof(MetadataInfo).Assembly;
            }

            return assembly != null;
        }

        private class ApiHubFileScriptBinding : ScriptBinding
        {
            private readonly ApiHubConfiguration _apiHubConfig;

            public ApiHubFileScriptBinding(ApiHubConfiguration apiHubConfig, ScriptBindingContext context) : base(context)
            {
                _apiHubConfig = apiHubConfig;
            }

            public override Type DefaultType
            {
                get
                {
                    return typeof(Stream);
                }
            }

            public override Collection<Attribute> GetAttributes()
            {
                Collection<Attribute> attributes = new Collection<Attribute>();

                string connectionStringSetting = Context.GetMetadataValue<string>("connection");
                if (!string.IsNullOrEmpty(connectionStringSetting))
                {
                    // Register each binding connection with the global config
                    string connectionStringValue = GetAppSettingOrEnvironmentValue(connectionStringSetting);
                    _apiHubConfig.AddConnection(connectionStringSetting, connectionStringValue);
                }

                string path = Context.GetMetadataValue<string>("path");
                if (Context.IsTrigger)
                {
                    FileWatcherType fileWatcherType = Context.GetMetadataEnumValue<FileWatcherType>("fileWatcherType", FileWatcherType.Created);
                    int pollIntervalInSeconds = Context.GetMetadataValue<int>("pollIntervalInSeconds");

                    attributes.Add(new ApiHubFileTriggerAttribute(connectionStringSetting, path, fileWatcherType, pollIntervalInSeconds));
                }
                else
                {
                    attributes.Add(new ApiHubFileAttribute(connectionStringSetting, path, Context.Access));
                }

                return attributes;
            }

            // TODO: Helper for this, or otherwise remove need for it
            private static string GetAppSettingOrEnvironmentValue(string name)
            {
                // first check app settings
                string value = ConfigurationManager.AppSettings[name];
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }

                // Check environment variables
                value = Environment.GetEnvironmentVariable(name);
                if (value != null)
                {
                    return value;
                }

                return null;
            }
        }

        private class ApiHubTableScriptBinding : ScriptBinding
        {
            public ApiHubTableScriptBinding(ScriptBindingContext context) : base(context)
            {
            }

            public override Type DefaultType
            {
                get
                {
                    if (Context.Access == FileAccess.Read)
                    {
                        return typeof(JObject);
                    }
                    else
                    {
                        return typeof(IAsyncCollector<JObject>);
                    }
                }
            }

            public override Collection<Attribute> GetAttributes()
            {
                var connection = Context.GetMetadataValue<string>("connection");
                return new Collection<Attribute>()
                {
                    new ApiHubTableAttribute(connection)
                    {
                        DataSetName = Context.GetMetadataValue<string>("dataSetName"),
                        EntityId = Context.GetMetadataValue<string>("entityId"),
                        TableName = Context.GetMetadataValue<string>("tableName")
                    }
                };
            }
        }
    }
}
