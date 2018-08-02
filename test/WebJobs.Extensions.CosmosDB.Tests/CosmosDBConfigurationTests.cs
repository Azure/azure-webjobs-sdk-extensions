// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB
{
    public class CosmosDBConfigurationTests
    {
        [Fact]
        public async Task Configuration_Caches_Clients()
        {
            // Arrange            
            var options = new CosmosDBOptions
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=c29tZV9rZXk=;",
            };
            var provider = CreateProvider("Default", options);
            var attribute = new CosmosDBAttribute { Id = "abcdef" };

            // Act
            var context1 = provider.CreateContext(attribute);
            var context2 = provider.CreateContext(attribute);
            var binder = await provider.BindForItemAsync(attribute, typeof(Item));

            // Assert
            Assert.Single(provider.ClientCache);
        }

        [Fact]
        public void Resolve_UsesAttribute_First()
        {
            var options = new CosmosDBOptions
            {
                ConnectionString = "Config"
            };
            var provider = CreateProvider("Default", options);

            // Act
            var connString = provider.ResolveConnectionString("Attribute");

            // Assert
            Assert.Equal("Attribute", connString);
        }

        [Fact]
        public void Resolve_UsesConfig_Second()
        {
            var options = new CosmosDBOptions
            {
                ConnectionString = "Config"
            };
            var provider = CreateProvider("Default", options);

            // Act
            var connString = provider.ResolveConnectionString(null);

            // Assert
            Assert.Equal("Config", connString);
        }

        [Fact]
        public void Resolve_UsesDefault_Last()
        {
            var provider = CreateProvider("Default");

            // Act
            var connString = provider.ResolveConnectionString(null);

            // Assert
            Assert.Equal("Default", connString);
        }

        [Theory]
        [InlineData(typeof(IEnumerable<string>), true)]
        [InlineData(typeof(IEnumerable<Document>), true)]
        [InlineData(typeof(IEnumerable<JObject>), true)]
        [InlineData(typeof(JArray), false)]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(List<Document>), false)]
        public void TryGetEnumerableType(Type type, bool expectedResult)
        {
            bool actualResult = CosmosDBExtensionConfigProvider.IsSupportedEnumerable(type);
            Assert.Equal(expectedResult, actualResult);
        }

        private CosmosDBExtensionConfigProvider CreateProvider(string defaultConnStr, CosmosDBOptions options = null)
        {
            options = options ?? new CosmosDBOptions();

            var nameResolver = new TestNameResolver();
            var loggerProvider = new TestLoggerProvider();
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);
            Mock<IOptions<CosmosDBOptions>> mockOptions = new Mock<IOptions<CosmosDBOptions>>();
            var configProvider = new CosmosDBExtensionConfigProvider(mockOptions.Object, nameResolver, loggerFactory);

            return configProvider;
        }
    }
}
