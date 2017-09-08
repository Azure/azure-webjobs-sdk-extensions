// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBUtilityTests
    {
        [Fact]
        public async Task CreateIfNotExists_DoesNotSetThroughput_IfZero()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var context = DocumentDBTestUtility.CreateContext(mockService.Object, throughput: 0);
            DocumentDBTestUtility.SetupDatabaseMock(mockService);
            DocumentDBTestUtility.SetupCollectionMock(mockService);

            // Act
            await DocumentDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExists_SetsPartitionKey_IfSpecified()
        {
            // Arrange
            string partitionKeyPath = "partitionKey";
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var context = DocumentDBTestUtility.CreateContext(mockService.Object, partitionKeyPath: partitionKeyPath);
            DocumentDBTestUtility.SetupDatabaseMock(mockService);
            DocumentDBTestUtility.SetupCollectionMock(mockService, partitionKeyPath);

            // Act
            await DocumentDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            DocumentDBContext context = DocumentDBTestUtility.CreateContext(mockService.Object);
            DocumentDBTestUtility.SetupDatabaseMock(mockService);
            DocumentDBTestUtility.SetupCollectionMock(mockService);

            // Act
            await DocumentDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Rethrows()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            DocumentDBContext context = DocumentDBTestUtility.CreateContext(mockService.Object);
            DocumentDBTestUtility.SetupDatabaseMock(mockService);

            // overwrite the default setup with one that throws
            mockService
                .Setup(m => m.CreateDatabaseIfNotExistsAsync(It.Is<Database>(d => d.Id == DocumentDBTestUtility.DatabaseName)))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.BadRequest));

            // Act
            await Assert.ThrowsAsync<DocumentClientException>(
                () => DocumentDBUtility.CreateDatabaseAndCollectionIfNotExistAsync(context));

            // Assert            
            mockService.VerifyAll();
        }
    }
}
