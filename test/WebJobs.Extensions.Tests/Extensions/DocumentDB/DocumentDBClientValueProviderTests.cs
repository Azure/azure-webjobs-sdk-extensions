// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;

using System;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.Documents.Client;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBClientValueProviderTests
    {
        [Fact]
        public void GetValue_Returns_CorrectClient()
        {
            // Arrange
            var client = new DocumentClient(new Uri("https://someuri/"), "my_auth_key");
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            mockService
                .Setup(m => m.GetClient())
                .Returns(client);

            var context = new DocumentDBContext
            {
                Service = mockService.Object
            };

            var provider = new DocumentDBClientValueProvider(context);

            // Act
            var providerClient = provider.GetValue() as DocumentClient;

            // Assert
            Assert.NotNull(providerClient);
            Assert.Same(client, providerClient);
        }
    }
}
