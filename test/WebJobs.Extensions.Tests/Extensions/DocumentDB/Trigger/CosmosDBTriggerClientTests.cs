// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Trigger
{
    public class CosmosDBTriggerClientTests
    {
        private const string DatabaseName = "ItemDB";
        private const string CollectionName = "ItemCollection";
        private readonly Uri databaseUri = UriFactory.CreateDatabaseUri(DatabaseName);

        [Fact]
        public async Task CreateIfNotExists_DoesNotSetThroughput_IfZero()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            SetupCollectionMock(mockService);

            // Act
            await CosmosDBTriggerAttributeBindingProvider.CreateDocumentCollectionIfNotExistsAsync(mockService.Object, DatabaseName,
                CollectionName, 0);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            SetupDatabaseMock(mockService);
            SetupCollectionMock(mockService);

            // Act
            await CosmosDBTriggerAttributeBindingProvider.CreateIfNotExistAsync(mockService.Object, DatabaseName, CollectionName, 0);

            // Assert
            mockService.VerifyAll();
        }
        
        private void SetupCollectionMock(Mock<IDocumentDBService> mockService,
            int throughput = 0)
        {
            if (throughput == 0)
            {
                mockService
                   .Setup(m => m.CreateDocumentCollectionIfNotExistsAsync(databaseUri, 
                        It.Is<DocumentCollection>(d => d.Id == CollectionName ), 
                        null))
                   .ReturnsAsync(new DocumentCollection { Id = CollectionName });
            }
            else
            {
                mockService
                   .Setup(m => m.CreateDocumentCollectionIfNotExistsAsync(databaseUri, 
                        It.Is<DocumentCollection>(d => d.Id == CollectionName),
                        It.Is<RequestOptions>(r => r.OfferThroughput == throughput)))
                   .ReturnsAsync(new DocumentCollection { Id = CollectionName });
            }
        }

        private void SetupDatabaseMock(Mock<IDocumentDBService> mockService)
        {
            mockService
                .Setup(m => m.CreateDatabaseIfNotExistsAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ReturnsAsync(new Database());
        }
    }
}
