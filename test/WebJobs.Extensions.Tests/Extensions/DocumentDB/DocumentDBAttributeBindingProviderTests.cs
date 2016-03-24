// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DocumentDB::Microsoft.Azure.WebJobs;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBAttributeBindingProviderTests
    {
        private const string DatabaseName = "TestDatabase";
        private const string CollectionName = "TestCollection";
        private readonly Uri databaseUri = new Uri("dbs/" + DatabaseName, UriKind.Relative);

        public static IEnumerable<object[]> ValidParameters
        {
            get
            {
                var validParams = DocumentDBTestUtility.GetValidOutputParameters()
                    .Concat(DocumentDBTestUtility.GetValidItemInputParameters())
                    .Concat(DocumentDBTestUtility.GetValidClientInputParameters());

                return validParams.Select(p => new object[] { p });
            }
        }

        [Theory]
        [MemberData("ValidParameters")]
        public async Task TryCreateAsync_CreatesBinding_ForValidParameters(ParameterInfo parameter)
        {
            // Act
            var binding = await CreateProviderAndTryCreateAsync(parameter);

            // Assert
            Assert.NotNull(binding);
            Assert.Equal(GetExpectedBindingType(parameter.ParameterType), binding.GetType());
        }

        private Type GetExpectedBindingType(Type parameterType)
        {
            if (parameterType.IsByRef)
            {
                Type t = parameterType.GetElementType();

                if (t.IsArray)
                {
                    t = t.GetElementType();
                }

                return DocumentDBTestUtility.GetAsyncCollectorType(t);
            }

            if (parameterType.IsGenericType &&
                (parameterType.GetGenericTypeDefinition() == typeof(ICollector<>) ||
                parameterType.GetGenericTypeDefinition() == typeof(IAsyncCollector<>)))
            {
                Type t = parameterType.GetGenericArguments()[0];
                return DocumentDBTestUtility.GetAsyncCollectorType(t);
            }

            if (parameterType == typeof(DocumentClient))
            {
                return typeof(DocumentDBClientBinding);
            }

            return typeof(DocumentDBItemBinding);
        }

        private static Task<IBinding> CreateProviderAndTryCreateAsync(ParameterInfo parameter)
        {
            var jobConfig = new JobHostConfiguration();
            var docDBConfig = new DocumentDBConfiguration
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=my_key"
            };
            var provider = new DocumentDBAttributeBindingProvider(jobConfig, docDBConfig);

            var context = new BindingProviderContext(parameter, null, CancellationToken.None);

            return provider.TryCreateAsync(context);
        }

        [Fact]
        public async Task CreateIfNotExists_DoesNotCreate_IfFalse()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var config = new DocumentDBConfiguration
            {
                ConnectionString = "AccountEndpoint=http://someuri;AccountKey=some_key",
                DocumentDBServiceFactory = new TestDocumentDBServiceFactory(mockService.Object)
            };
            var attribute = new DocumentDBAttribute { CreateIfNotExists = false };
            var provider = new DocumentDBAttributeBindingProvider(new JobHostConfiguration(), config);

            // Act
            await provider.TryCreateAsync(new BindingProviderContext(DocumentDBTestUtility.GetCreateIfNotExistsParameters().First(), null, CancellationToken.None));

            // Assert
            // Nothing to assert. Since the service was null, it was never called.
        }

        [Fact]
        public async Task CreateIfNotExists_Creates_IfTrue()
        {
            // Arrange            
            string databaseName = "TestDB";
            string collectionName = "TestCollection";

            var databaseUri = UriFactory.CreateDatabaseUri(databaseName);

            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == databaseName)))
                .ReturnsAsync(new Database());

            mockService
                .Setup(m => m.CreateDocumentCollectionAsync(databaseUri, It.Is<DocumentCollection>(d => d.Id == collectionName)))
                .ReturnsAsync(new DocumentCollection());

            var config = new DocumentDBConfiguration
            {
                ConnectionString = "AccountEndpoint=http://someuri;AccountKey=some_key",
                DocumentDBServiceFactory = new TestDocumentDBServiceFactory(mockService.Object)
            };

            var provider = new DocumentDBAttributeBindingProvider(new JobHostConfiguration(), config);

            // Act
            await provider.TryCreateAsync(new BindingProviderContext(DocumentDBTestUtility.GetCreateIfNotExistsParameters().Last(), null, CancellationToken.None));

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfDbAndCollectionDoNotExist()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ReturnsAsync(new Database());

            mockService
                .Setup(m => m.CreateDocumentCollectionAsync(databaseUri, It.Is<DocumentCollection>(d => d.Id == CollectionName)))
                .ReturnsAsync(new DocumentCollection());

            // Act
            await DocumentDBAttributeBindingProvider.CreateIfNotExistAsync(mockService.Object, DatabaseName, CollectionName);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfCollectionDoesNotExist()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.Conflict));

            mockService
                .Setup(m => m.CreateDocumentCollectionAsync(databaseUri, It.Is<DocumentCollection>(d => d.Id == CollectionName)))
                .ReturnsAsync(new DocumentCollection());

            // Act
            await DocumentDBAttributeBindingProvider.CreateIfNotExistAsync(mockService.Object, DatabaseName, CollectionName);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfDbAndCollectionExist()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.Conflict));

            mockService
                .Setup(m => m.CreateDocumentCollectionAsync(databaseUri, It.Is<DocumentCollection>(d => d.Id == CollectionName)))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.Conflict));

            // Act
            await DocumentDBAttributeBindingProvider.CreateIfNotExistAsync(mockService.Object, DatabaseName, CollectionName);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Throws_IfExceptionIsNotConflict()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.BadRequest));

            // Act
            await Assert.ThrowsAsync<DocumentClientException>(
                () => DocumentDBAttributeBindingProvider.CreateIfNotExistAsync(mockService.Object, DatabaseName, CollectionName));

            // Assert            
            mockService.VerifyAll();
        }

        [Fact]
        public void CreateContext_ResolvesNames()
        {
            // Arrange
            var resolver = new TestNameResolver();
            resolver.Values.Add("MyDatabase", "123abc");
            resolver.Values.Add("MyCollection", "abc123");

            var attribute = new DocumentDBAttribute("%MyDatabase%", "%MyCollection%");

            var config = new DocumentDBConfiguration
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=some_key"
            };

            // Act
            var context = DocumentDBAttributeBindingProvider.CreateContext(config, attribute, resolver);

            // Assert
            Assert.Equal("123abc", context.ResolvedDatabaseName);
            Assert.Equal("abc123", context.ResolvedCollectionName);
        }

        [Theory]
        [InlineData("MyDocumentDBConnectionString", "AccountEndpoint=https://fromappsetting;AccountKey=some_key")]
        [InlineData(null, "AccountEndpoint=https://fromconnstrings;AccountKey=some_key")]
        [InlineData("", "AccountEndpoint=https://fromconnstrings;AccountKey=some_key")]
        public void CreateContext_AttributeUri_Wins(string attributeConnection, string expectedConnection)
        {
            // Arrange            
            var attribute = new DocumentDBAttribute
            {
                ConnectionString = attributeConnection
            };

            var mockFactory = new Mock<IDocumentDBServiceFactory>();
            mockFactory
                .Setup(f => f.CreateService(expectedConnection))
                .Returns<IDocumentDBService>(null);

            // Default ConnecitonString will come from app.config
            var config = new DocumentDBConfiguration
            {
                DocumentDBServiceFactory = mockFactory.Object
            };

            // Act
            DocumentDBAttributeBindingProvider.CreateContext(config, attribute, null);

            // Assert
            mockFactory.VerifyAll();
        }
    }
}
