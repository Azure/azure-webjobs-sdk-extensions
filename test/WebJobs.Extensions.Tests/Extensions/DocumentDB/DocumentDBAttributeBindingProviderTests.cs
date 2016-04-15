// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DocumentDB::Microsoft.Azure.WebJobs;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBAttributeBindingProviderTests
    {
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
            var provider = new DocumentDBAttributeBindingProvider(jobConfig, docDBConfig, new TestTraceWriter());

            var context = new BindingProviderContext(parameter, null, CancellationToken.None);

            return provider.TryCreateAsync(context);
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
            var context = DocumentDBAttributeBindingProvider.CreateContext(config, attribute, resolver, new TestTraceWriter());

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
            DocumentDBAttributeBindingProvider.CreateContext(config, attribute, null, new TestTraceWriter());

            // Assert
            mockFactory.VerifyAll();
        }

        [Fact]
        public void CreateContext_UsesDefaultRetryValue()
        {
            // Arrange            
            var attribute = new DocumentDBAttribute();
            var config = new DocumentDBConfiguration();

            // Act
            var context = DocumentDBAttributeBindingProvider.CreateContext(config, attribute, null, new TestTraceWriter());

            // Assert
            Assert.Equal(10, context.MaxThrottleRetries);
        }
    }
}
