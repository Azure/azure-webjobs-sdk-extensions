// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind a parameter to an ApiHub table or entity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ApiHubTableAttribute : Attribute
    {
        /// <summary>
        /// Creates a new instance of this class.
        /// </summary>
        /// <param name="connection">The key of the configuration setting that 
        /// specifies the connection string.</param>
        public ApiHubTableAttribute(string connection)
        {
            if (string.IsNullOrEmpty(connection))
            {
                throw new ArgumentException("The connection must not be null or empty.", "connection");
            }

            Connection = connection;
        }

        /// <summary>
        /// Gets the key of the configuration setting that specifies the connection string.
        /// </summary>
        public string Connection { get; }

        /// <summary>
        /// Gets or sets the data set name.
        /// </summary>
        public string DataSetName { get; set; }

        /// <summary>
        /// Gets or sets the table name.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Gets or sets the entity identifier.
        /// </summary>
        public string EntityId { get; set; }
    }
}
