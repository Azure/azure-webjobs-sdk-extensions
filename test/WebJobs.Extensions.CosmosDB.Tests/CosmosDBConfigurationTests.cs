// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBConfigurationTests
    {
        private static readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();

        [Fact]
        public async Task Configuration_Caches_Clients()
        {
            // Arrange
            var options = new CosmosDBOptions { ConnectionString = "AccountEndpoint=https://someuri;AccountKey=c29tZV9rZXk=;" };
            var config = new CosmosDBExtensionConfigProvider(new OptionsWrapper<CosmosDBOptions>(options), new DefaultCosmosDBServiceFactory(), _emptyConfig, new TestNameResolver(), NullLoggerFactory.Instance);
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
            var config = InitializeExtensionConfigProvider("Default", "Config");

            // Act
            var connString = config.ResolveConnectionString("Attribute");

            // Assert
            Assert.Equal("Attribute", connString);
        }

        [Fact]
        public void Resolve_UsesConfig_Second()
        {
            var config = InitializeExtensionConfigProvider("Default", "Config");

            // Act
            var connString = config.ResolveConnectionString(null);

            // Assert
            Assert.Equal("Config", connString);
        }

        [Fact]
        public void Resolve_UsesDefault_Last()
        {
            var config = InitializeExtensionConfigProvider("Default");

            // Act
            var connString = config.ResolveConnectionString(null);

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

        private CosmosDBExtensionConfigProvider InitializeExtensionConfigProvider(string defaultConnStr, string optionsConnStr = null)
        {
            var options = CosmosDBTestUtility.InitializeOptions(defaultConnStr, optionsConnStr);
            var factory = new DefaultCosmosDBServiceFactory();
            var nameResolver = new TestNameResolver();
            var configProvider = new CosmosDBExtensionConfigProvider(options, factory, _emptyConfig, nameResolver, NullLoggerFactory.Instance);

            var context = TestHelpers.CreateExtensionConfigContext(nameResolver);

            configProvider.Initialize(context);

            return configProvider;
        }
    }
}