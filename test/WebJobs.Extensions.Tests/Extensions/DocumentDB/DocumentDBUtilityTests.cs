// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBUtilityTests
    {
        [Fact]
        public async Task RetryAsync_Ignores_SpecifiedStatusCode()
        {
            // Arrange
            string testString = "Method called!";
            string s = null;
            var ex = DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.Conflict);

            // Act
            await DocumentDBUtility.RetryAsync<object>(() =>
            {
                s = testString;
                throw ex;
            }, 0, HttpStatusCode.Conflict, HttpStatusCode.NotFound);

            // Assert
            Assert.Equal(testString, s);
            // The fact that it doesn't throw proves that it was ignored
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new HttpStatusCode[] { HttpStatusCode.Conflict, HttpStatusCode.NotFound })]
        public async Task RetryAsync_DoesNotIgnore_OtherStatusCodes(HttpStatusCode[] codesToIgnore)
        {
            // Arrange
            string testString = "Method called!";
            string s = null;
            var ex = DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.ServiceUnavailable);

            // Act
            var thrownEx = await Assert.ThrowsAsync<DocumentClientException>(() =>
            {
                return DocumentDBUtility.RetryAsync<object>(() =>
                {
                    s = testString;
                    throw ex;
                }, 0, codesToIgnore);
            });

            // Assert
            Assert.Equal(testString, s);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, thrownEx.StatusCode);
        }

        [Fact]
        public async Task RetryAsync_Retries_IfStatusCode429()
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
            var result = await DocumentDBUtility.RetryAsync(() =>
            {
                return mockService.Object.UpsertDocumentAsync(mockUri, mockItem);
            }, 3, HttpStatusCode.NotFound);

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
        public async Task RetryAsync_MaxRetries(int maxRetries)
        {
            // Arrange
            var mockUri = new Uri("https://someuri");
            var mockItem = new Item();
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var tooManyRequestException = DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429);
            mockService.Setup(m => m.UpsertDocumentAsync(mockUri, mockItem)).Throws(tooManyRequestException);

            // Act
            var docEx = await Assert.ThrowsAsync<DocumentClientException>(() =>
                DocumentDBUtility.RetryAsync(() =>
                {
                    return mockService.Object.UpsertDocumentAsync(mockUri, mockItem);
                }, maxRetries));

            // Assert
            Assert.Same(tooManyRequestException, docEx);
            mockService.Verify(m => m.UpsertDocumentAsync(mockUri, mockItem), Times.Exactly(maxRetries + 1));
        }

        [Fact]
        public async Task RetryAsync_Throws_IfErrorStatusCode()
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
                return DocumentDBUtility.RetryAsync(() =>
                {
                    return mockService.Object.UpsertDocumentAsync(mockUri, mockItem);
                }, 3);
            });

            // Assert
            mockService.VerifyAll();
            Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        }
    }
}
