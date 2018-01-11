// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.ApiHub;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    /// <summary>
    /// Represents a factory to create ApiHub connections.
    /// </summary>
    public class ConnectionFactory
    {
        private static ConnectionFactory _connectionFactory = new ConnectionFactory();
        private readonly INameResolver _nameResolver;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        protected ConnectionFactory()
        {
            _nameResolver = new DefaultNameResolver();
        }

        /// <summary>
        /// Gets or sets the default ConnectionFactory instance.
        /// </summary>
        public static ConnectionFactory Default
        {
            get { return _connectionFactory; }
            set { _connectionFactory = value; }
        }

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

            var connectionString = _nameResolver.Resolve(key);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(string.Format(
                    "The connection string with key '{0}' was not found in app settings or environment variables.", key));
            }

            return new Connection(connectionString);
        }
    }
}
