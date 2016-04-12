// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
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

            // make sure that we sleep here
            var tooManyRequestException = DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429, 1000);
            var document = new Document();

            mockService
                .SetupSequence(m => m.UpsertDocumentAsync(mockUri, mockItem))
                .Throws(tooManyRequestException)
                .Throws(tooManyRequestException)
                .Returns(Task.FromResult(document));

            var start = DateTime.UtcNow;

            // Act
            var result = await DocumentDBUtility.ExecuteWithRetriesAsync(() =>
            {
                return mockService.Object.UpsertDocumentAsync(mockUri, mockItem);
            }, 3);

            // Assert
            var stop = DateTime.UtcNow;
            Assert.True((stop - start).TotalMilliseconds >= 2000);
            Assert.Same(document, result);
            mockService.Verify(m => m.UpsertDocumentAsync(mockUri, mockItem), Times.Exactly(3));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(10)]
        public async Task ExecuteWithRetriesAsync_MaxRetries(int maxRetries)
        {
            // Arrange
            var mockUri = new Uri("https://someuri");
            var mockItem = new Item();
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var tooManyRequestException = DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429);
            mockService.Setup(m => m.UpsertDocumentAsync(mockUri, mockItem)).Throws(tooManyRequestException);            

            // Act
            var docEx = await Assert.ThrowsAsync<DocumentClientException>(() =>
                DocumentDBUtility.ExecuteWithRetriesAsync(() =>
                {
                    return mockService.Object.UpsertDocumentAsync(mockUri, mockItem);
                }, maxRetries));

            // Assert
            Assert.Same(tooManyRequestException, docEx);
            mockService.Verify(m => m.UpsertDocumentAsync(mockUri, mockItem), Times.Exactly(maxRetries + 1));
        }

        [Fact]
        public async Task ExecuteWithRetriesAsync_Throws_IfErrorStatusCode()
        {
            // Arrange
            var mockUri = new Uri("https://someuri");
            var mockItem = new Item();
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var notFoundException = DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound);

            mockService
                .Setup(m => m.UpsertDocumentAsync(mockUri, mockItem))
                .Throws(notFoundException);

            // Act
            var ex = await Assert.ThrowsAsync<DocumentClientException>(() =>
            {
                return DocumentDBUtility.ExecuteWithRetriesAsync(() =>
                {
                    return mockService.Object.UpsertDocumentAsync(mockUri, mockItem);
                }, 3);
            });

            // Assert
            mockService.VerifyAll();
            Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        }

        [Fact]
        public async Task ExecuteWithRetriesAsync_CorrectlyIgnoresNotFound()
        {
            // Arrange
            var mockUri = new Uri("https://someuri");
            var mockItem = new Item();
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var tooManyRequestException = DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429);
            var notFoundException = DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound);

            mockService
               .SetupSequence(m => m.UpsertDocumentAsync(mockUri, mockItem))
               .Throws(tooManyRequestException)
               .Throws(tooManyRequestException)
               .Throws(tooManyRequestException)
               .Throws(notFoundException);

            // Act
            var result = await DocumentDBUtility.ExecuteWithRetriesAsync(() =>
                {
                    return mockService.Object.UpsertDocumentAsync(mockUri, mockItem);
                }, 3, ignoreNotFound: true);

            // Assert
            Assert.Null(result);
            mockService.Verify(m => m.UpsertDocumentAsync(mockUri, mockItem), Times.Exactly(4));
        }
    }
}
