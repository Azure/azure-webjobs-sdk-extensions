// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Microsoft.Azure.WebJobs.Host.Config;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBConfigurationTests
    {
        [Fact]
        public async Task Configuration_Caches_Clients()
        {
            // Arrange            
            var config = new DocumentDBConfiguration
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=c29tZV9rZXk=;",
            };
            var attribute = new DocumentDBAttribute();

            // Act
            var context1 = config.CreateContext(attribute);
            var context2 = config.CreateContext(attribute);
            var binder = await config.BindForItemAsync(attribute, typeof(Item));

            // Assert
            Assert.Equal(1, config.ClientCache.Count);
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

        [Fact]
        public void CreateContext_UsesDefaultRetryValue()
        {
            // Arrange            
            var attribute = new DocumentDBAttribute
            {
                ConnectionStringSetting = "AccountEndpoint=https://someuri;AccountKey=c29tZV9rZXk=;"
            };
            var config = new DocumentDBConfiguration();

            // Act
            var context = config.CreateContext(attribute);

            // Assert
            Assert.Equal(DocumentDBContext.DefaultMaxThrottleRetries, context.MaxThrottleRetries);
        }

        private DocumentDBConfiguration InitializeConfig(string defaultConnStr)
        {
            var config = new DocumentDBConfiguration();

            var nameResolver = new TestNameResolver();
            nameResolver.Values[DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName] = defaultConnStr;

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
