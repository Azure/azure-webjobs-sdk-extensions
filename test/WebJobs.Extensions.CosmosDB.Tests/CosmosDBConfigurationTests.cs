// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Azure.WebJobs.Host.Config;
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
            var config = new CosmosDBConfiguration
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=c29tZV9rZXk=;",
            };
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
            var config = InitializeConfig("Default");
            config.ConnectionString = "Config";

            // Act
            var connString = config.ResolveConnectionString("Attribute");

            // Assert
            Assert.Equal("Attribute", connString);
        }

        [Fact]
        public void Resolve_UsesConfig_Second()
        {
            var config = InitializeConfig("Default");
            config.ConnectionString = "Config";

            // Act
            var connString = config.ResolveConnectionString(null);

            // Assert
            Assert.Equal("Config", connString);
        }

        [Fact]
        public void Resolve_UsesDefault_Last()
        {
            var config = InitializeConfig("Default");

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
            bool actualResult = CosmosDBConfiguration.IsSupportedEnumerable(type);
            Assert.Equal(expectedResult, actualResult);
        }

        private CosmosDBConfiguration InitializeConfig(string defaultConnStr)
        {
            var config = new CosmosDBConfiguration();

            var nameResolver = new TestNameResolver();
            nameResolver.Values[CosmosDBConfiguration.AzureWebJobsCosmosDBConnectionStringName] = defaultConnStr;

            var jobHostConfig = new JobHostConfiguration();
            jobHostConfig.AddService<INameResolver>(nameResolver);

            var context = new ExtensionConfigContext()
            {
                Config = jobHostConfig
            };

            config.Initialize(context);

            return config;
        }
    }
}
