// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;

using System;
using System.Net;
using System.Threading.Tasks;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBUtilityTests
    {
        [Fact]
        public async Task ExecuteAndIgnoreStatusCode_Ignores_SpecifiedStatusCode()
        {
            // Arrange
            string testString = "Method called!";
            string s = null;
            var ex = DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.Conflict);

            // Act
            await DocumentDBUtility.ExecuteAndIgnoreStatusCodeAsync(HttpStatusCode.Conflict, () =>
            {
                s = testString;
                throw ex;
            });

            // Assert
            Assert.Equal(testString, s);
            // The fact that it doesn't throw proves that it was ignored
        }

        [Fact]
        public async Task ExecuteAndIgnoreStatusCode_DoesNotIgnore_OtherStatusCodes()
        {
            // Arrange
            string testString = "Method called!";
            string s = null;
            var ex = DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.ServiceUnavailable);

            // Act
            var thrownEx = await Assert.ThrowsAsync<DocumentClientException>(() =>
            {
                return DocumentDBUtility.ExecuteAndIgnoreStatusCodeAsync(HttpStatusCode.Conflict, () =>
                {
                    s = testString;
                    throw ex;
                });
            });

            // Assert
            Assert.Equal(testString, s);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, thrownEx.StatusCode);
        }

        [Fact]
        public async Task ExecuteWithRetriesAsync_Retries_IfStatusCode429()
        {
            // Arrange
            var mockUri = new Uri("https://someuri");
            var mockItem = new Item();
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var tooManyRequestException = DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429);

            mockService
                .SetupSequence(m => m.CreateDocumentAsync(mockUri, mockItem))
                .Throws(tooManyRequestException)
                .Throws(tooManyRequestException)
                .Returns(Task.FromResult(new Document()));

            // Act
            await DocumentDBUtility.ExecuteWithRetriesAsync(() =>
            {
                return mockService.Object.CreateDocumentAsync(mockUri, mockItem);
            });

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task ExecuteWithRetriesAsync_Throws_IfErrorStatusCode()
        {
            // Arrange
            var mockUri = new Uri("https://someuri");
            var mockItem = new Item();
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var serviceUnavailableException = DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.ServiceUnavailable);

            mockService
                .Setup(m => m.CreateDocumentAsync(mockUri, mockItem))
                .Throws(serviceUnavailableException);

            // Act
            var ex = await Assert.ThrowsAsync<DocumentClientException>(() =>
            {
                return DocumentDBUtility.ExecuteWithRetriesAsync(() =>
                {
                    return mockService.Object.CreateDocumentAsync(mockUri, mockItem);
                });
            });

            // Assert
            mockService.VerifyAll();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        }
    }
}
