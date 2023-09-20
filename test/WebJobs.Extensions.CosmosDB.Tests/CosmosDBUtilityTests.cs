// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
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
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);
            var context = CosmosDBTestUtility.CreateContext(mockService.Object, throughput: 0);
            CosmosDBTestUtility.SetupCollectionMock(mockService, CosmosDBTestUtility.SetupDatabaseMock(mockService), "/id");

            // Act
            await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExists_SetsThroughput()
        {
            // Arrange
            int throughput = 1000;
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);
            var context = CosmosDBTestUtility.CreateContext(mockService.Object, throughput: throughput);
            CosmosDBTestUtility.SetupCollectionMock(mockService, CosmosDBTestUtility.SetupDatabaseMock(mockService), "/id", throughput: throughput);

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
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);
            var context = CosmosDBTestUtility.CreateContext(mockService.Object, partitionKeyPath: partitionKeyPath);
            CosmosDBTestUtility.SetupCollectionMock(mockService, CosmosDBTestUtility.SetupDatabaseMock(mockService), partitionKeyPath);

            // Act
            await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExists_SetsTTL_IfSpecified()
        {
            // Arrange
            string partitionKeyPath = "partitionKey";
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);
            
            CosmosDBTestUtility.SetupCollectionMock(mockService, CosmosDBTestUtility.SetupDatabaseMock(mockService), partitionKeyPath, setTTL: true);

            // Act
            await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(mockService.Object, CosmosDBTestUtility.DatabaseName, CosmosDBTestUtility.ContainerName, partitionKeyPath, throughput: 0, setTTL: true);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds()
        {
            // Arrange
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);
            CosmosDBContext context = CosmosDBTestUtility.CreateContext(mockService.Object);
            CosmosDBTestUtility.SetupCollectionMock(mockService, CosmosDBTestUtility.SetupDatabaseMock(mockService), null);

            // Act
            await CosmosDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Rethrows()
        {
            // Arrange
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);
            CosmosDBContext context = CosmosDBTestUtility.CreateContext(mockService.Object);
            CosmosDBTestUtility.SetupDatabaseMock(mockService);

            // overwrite the default setup with one that throws
            mockService
                .Setup(m => m.CreateDatabaseIfNotExistsAsync(It.Is<string>(d => d == CosmosDBTestUtility.DatabaseName), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.BadRequest));

            // Act
            await Assert.ThrowsAsync<CosmosException>(
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
            string userAgent = Guid.NewGuid().ToString();
            CosmosSerializer serializer = new CustomSerializer();

            // Act
            var policy = CosmosDBUtility.BuildClientOptions(ConnectionMode.Direct, serializer, preferredLocationsWithEntries, userAgent);

            // Assert
            Assert.Equal(userAgent, policy.ApplicationName);
            Assert.Equal(ConnectionMode.Direct, policy.ConnectionMode);
            Assert.Equal(serializer, policy.Serializer);
            Assert.Equal(2, policy.ApplicationPreferredRegions.Count);
        }

        [Fact]
        public void BuildConnectionPolicy_Defaults()
        {
            // Arrange
            // Act
            var policy = CosmosDBUtility.BuildClientOptions(
                connectionMode: null,
                serializer: null,
                preferredLocations: null,
                userAgent: null);

            // Assert
            Assert.Null(policy.ApplicationName);
            Assert.Equal(ConnectionMode.Gateway, policy.ConnectionMode);
            Assert.Null(policy.Serializer);
            Assert.Null(policy.ApplicationPreferredRegions);
        }

        private class CustomSerializer : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                throw new NotImplementedException();
            }

            public override Stream ToStream<T>(T input)
            {
                throw new NotImplementedException();
            }
        }
    }
}
