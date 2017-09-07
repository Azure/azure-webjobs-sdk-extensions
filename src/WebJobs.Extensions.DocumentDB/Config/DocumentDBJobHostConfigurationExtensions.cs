// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for DocumentDB integration.
    /// </summary>
    public static class DocumentDBJobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of the DocumentDB extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="documentDBConfig">The <see cref="DocumentDBConfiguration"/> to use.</param>
        public static void UseDocumentDB(this JobHostConfiguration config, DocumentDBConfiguration documentDBConfig = null)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (documentDBConfig == null)
            {
                documentDBConfig = new DocumentDBConfiguration();
            }

            config.RegisterExtensionConfigProvider(documentDBConfig);
        }
    }
}
