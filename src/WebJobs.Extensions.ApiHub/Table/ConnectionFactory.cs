// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Configuration;
using Microsoft.Azure.ApiHub.Sdk;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    /// <summary>
    /// Represents a factory to create ApiHub connections.
    /// </summary>
    public class ConnectionFactory
    {
        /// <summary>
        /// Gets or sets the default ConnectionFactory instance.
        /// </summary>
        public static ConnectionFactory Default { get; set; } = new ConnectionFactory();

        /// <summary>
        /// Creates a connection.
        /// </summary>
        /// <param name="key">The key of the configuration setting 
        /// that specifies the connection string.</param>
        /// <returns>The connection created.</returns>
        public virtual Connection CreateConnection(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The key must not be null or empty.", "key");
            }

            var connectionString = GetConnectionString(key);

            return new Connection(connectionString);
        }

        private static string GetConnectionString(string key)
        {
            var connectionString = GetSettingFromConfigOrEnvironment(key);

            if (connectionString != null)
            {
                return connectionString;
            }

            throw new InvalidOperationException(string.Format(
                "The connection string with key '{0}' was not found in app settings or environment variables.",
                key));
        }

        private static string GetSettingFromConfigOrEnvironment(string key)
        {
            var setting = ConfigurationManager.AppSettings[key];

            if (!string.IsNullOrEmpty(setting))
            {
                return setting;
            }

            setting = Environment.GetEnvironmentVariable(key);

            if (!string.IsNullOrEmpty(setting))
            {
                return setting;
            }

            return null;
        }
    }
}
