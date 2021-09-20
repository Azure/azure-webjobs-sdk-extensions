// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    // Very simple AzureStorageProvider for tests
    public class TestAzureStorageProvider : IAzureStorageProvider
    {
        private const string HostContainerName = "azure-webjobs-hosts";
        private readonly IConfiguration _configuration;

        public TestAzureStorageProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool ConnectionExists(string connection)
        {
            return _configuration.GetWebJobsExtensionConfigurationSection(ConnectionStringNames.Storage).Exists();
        }

        public BlobContainerClient GetWebJobsBlobContainerClient()
        {
            if (TryGetBlobServiceClientFromConnection(out BlobServiceClient client, ConnectionStringNames.Storage))
            {
                return client.GetBlobContainerClient(HostContainerName);
            }

            throw new InvalidOperationException("Could not create BlobContainerClient in TestAzureStorageProvider.");
        }

        public bool TryGetBlobServiceClientFromConnection(out BlobServiceClient client, string connection)
        {
            var connectionString = _configuration.GetWebJobsConnectionString(connection);
            try
            {
                client = new BlobServiceClient(connectionString);
                return true;
            }
            catch
            {
                throw;
            }
        }
    }
}
