// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB.Config;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    /// <summary>
    /// Defines the configuration options for the DocumentDB binding.
    /// </summary>
    public class DocumentDBConfiguration : IExtensionConfigProvider
    {
        internal const string AzureWebJobsDocumentDBConnectionStringName = "AzureWebJobsDocumentDBConnectionString";

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public DocumentDBConfiguration()
        {
            this.ConnectionString = Resolve(AzureWebJobsDocumentDBConnectionStringName);
            this.DocumentDBServiceFactory = new DefaultDocumentDBServiceFactory();
        }

        internal IDocumentDBServiceFactory DocumentDBServiceFactory { get; set; }

        /// <summary>
        /// Gets or sets the DocumentDB connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <inheritdoc />
        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            context.Config.RegisterBindingExtension(new DocumentDBAttributeBindingProvider(context.Config, this));
        }

        internal static string Resolve(string key)
        {
            string value = null;

            if (string.IsNullOrEmpty(value))
            {
                ConnectionStringSettings connectionString = ConfigurationManager.ConnectionStrings[key];
                if (connectionString != null)
                {
                    value = connectionString.ConnectionString;
                }

                if (string.IsNullOrEmpty(value))
                {
                    value = Environment.GetEnvironmentVariable(key);
                }
            }

            return value;
        }
    }
}
