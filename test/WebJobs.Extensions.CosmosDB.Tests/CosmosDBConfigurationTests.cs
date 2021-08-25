// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBConfigurationTests
    {
        private static readonly IConfiguration _baseConfig = CosmosDBTestUtility.BuildConfiguration(new List<Tuple<string, string>>() 
        {
            Tuple.Create(Constants.DefaultConnectionStringName, "AccountEndpoint=https://defaultUri;AccountKey=c29tZV9rZXk=;"),
            Tuple.Create("Attribute", "AccountEndpoint=https://attributeUri;AccountKey=c29tZV9rZXk=;")
        });

        [Fact]
        public async Task Configuration_Caches_Clients()
        {
            // Arrange
            var options = new CosmosDBOptions();
            var config = new CosmosDBExtensionConfigProvider(new OptionsWrapper<CosmosDBOptions>(options), new DefaultCosmosDBServiceFactory(_baseConfig, Mock.Of<AzureComponentFactory>()), new DefaultCosmosDBSerializerFactory(), new TestNameResolver(), NullLoggerFactory.Instance);
            var attribute = new CosmosDBAttribute { Id = "abcdef" };

            // Act
            var context1 = config.CreateContext(attribute);
            var context2 = config.CreateContext(attribute);
            var binder = await config.BindForItemAsync(attribute, typeof(Item));

            // Assert
            Assert.Single(config.ClientCache);
        }

        [Fact]
        public void Resolve_UsesAttribute_First()
        {
            var config = InitializeExtensionConfigProvider();

            // Act
            var context = config.CreateContext(new CosmosDBAttribute() { Connection = "Attribute" });

            // Assert
            Assert.True(context.Service.Endpoint.ToString().Contains("attribute"));
        }

        [Fact]
        public void Resolve_UsesDefault_Last()
        {
            var config = InitializeExtensionConfigProvider();

            // Act
            var context = config.CreateContext(new CosmosDBAttribute());

            // Assert
            Assert.True(context.Service.Endpoint.ToString().Contains("default"));
        }

        [Theory]
        [InlineData(typeof(IEnumerable<string>), true)]
        [InlineData(typeof(IEnumerable<Item>), true)]
        [InlineData(typeof(IEnumerable<JObject>), true)]
        [InlineData(typeof(JArray), false)]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(List<Item>), false)]
        public void TryGetEnumerableType(Type type, bool expectedResult)
        {
            bool actualResult = CosmosDBExtensionConfigProvider.IsSupportedEnumerable(type);
            Assert.Equal(expectedResult, actualResult);
        }

        private CosmosDBExtensionConfigProvider InitializeExtensionConfigProvider()
        {
            var options = new CosmosDBOptions();
            var factory = new DefaultCosmosDBServiceFactory(_baseConfig, Mock.Of<AzureComponentFactory>());
            var nameResolver = new TestNameResolver();
            var configProvider = new CosmosDBExtensionConfigProvider(new OptionsWrapper<CosmosDBOptions>(options), factory, new DefaultCosmosDBSerializerFactory(), nameResolver, NullLoggerFactory.Instance);

            var context = TestHelpers.CreateExtensionConfigContext(nameResolver);

            configProvider.Initialize(context);

            return configProvider;
        }
    }
}