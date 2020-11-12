﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBUtilityTests
    {
        [Fact]
        public async Task CreateIfNotExists_DoesNotSetThroughput_IfZero()
        {
            // Arrange
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            var context = CosmosDBTestUtility.CreateContext(mockService.Object, throughput: 0);
            CosmosDBTestUtility.SetupDatabaseMock(mockService);
            CosmosDBTestUtility.SetupCollectionMock(mockService);

            // Act
            await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExists_SetsPartitionKey_IfSpecified()
        {
            // Arrange
            string partitionKeyPath = "partitionKey";
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            var context = CosmosDBTestUtility.CreateContext(mockService.Object, partitionKeyPath: partitionKeyPath);
            CosmosDBTestUtility.SetupDatabaseMock(mockService);
            CosmosDBTestUtility.SetupCollectionMock(mockService, partitionKeyPath);

            // Act
            await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds()
        {
            // Arrange
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            CosmosDBContext context = CosmosDBTestUtility.CreateContext(mockService.Object);
            CosmosDBTestUtility.SetupDatabaseMock(mockService);
            CosmosDBTestUtility.SetupCollectionMock(mockService);

            // Act
            await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Rethrows()
        {
            // Arrange
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            CosmosDBContext context = CosmosDBTestUtility.CreateContext(mockService.Object);
            CosmosDBTestUtility.SetupDatabaseMock(mockService);

            // overwrite the default setup with one that throws
            mockService
                .Setup(m => m.CreateDatabaseIfNotExistsAsync(It.Is<Database>(d => d.Id == CosmosDBTestUtility.DatabaseName)))
                .ThrowsAsync(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.BadRequest));

            // Act
            await Assert.ThrowsAsync<DocumentClientException>(
                () => CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context));

            // Assert            
            mockService.VerifyAll();
        }

        [Fact]
        public void ParsePreferredLocations_WhenEmpty()
        {
            // Arrange
            string preferredLocationsEmpty = string.Empty;
            string preferredLocationsNull = null;

            // Act
            var parsedLocationsEmpty = CosmosDBUtility.ParsePreferredLocations(preferredLocationsEmpty);
            var parsedLocationsNull = CosmosDBUtility.ParsePreferredLocations(preferredLocationsNull);

            // Assert
            Assert.Empty(parsedLocationsEmpty);
            Assert.Empty(parsedLocationsNull);
        }

        [Fact]
        public void ParsePreferredLocations_WithEntries()
        {
            // Arrange
            string preferredLocationsWithEntries = "East US, North Europe,";

            // Act
            var parsedLocations = CosmosDBUtility.ParsePreferredLocations(preferredLocationsWithEntries);

            // Assert
            Assert.Equal(2, parsedLocations.Count());
        }

        [Fact]
        public void BuildConnectionPolicy()
        {
            // Arrange
            string preferredLocationsWithEntries = "East US, North Europe,";
            bool useMultiMaster = true;
            string userAgent = Guid.NewGuid().ToString();

            // Act
            var policy = CosmosDBUtility.BuildConnectionPolicy(Documents.Client.ConnectionMode.Direct, Documents.Client.Protocol.Tcp, preferredLocationsWithEntries, useMultiMaster, userAgent);

            // Assert
            Assert.Equal(userAgent, policy.UserAgentSuffix);
            Assert.Equal(useMultiMaster, policy.UseMultipleWriteLocations);
            Assert.Equal(Documents.Client.ConnectionMode.Direct, policy.ConnectionMode);
            Assert.Equal(Documents.Client.Protocol.Tcp, policy.ConnectionProtocol);
            Assert.Equal(2, policy.PreferredLocations.Count);
        }
    }
}
