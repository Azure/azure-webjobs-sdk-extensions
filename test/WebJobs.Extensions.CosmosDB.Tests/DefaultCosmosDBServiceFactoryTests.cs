// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Azure.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class DefaultCosmosDBServiceFactoryTests
    {
        [Fact]
        public void UsesDefaultConnection()
        {
            // Arrange
            var config = CosmosDBTestUtility.BuildConfiguration(new List<Tuple<string, string>>()
            {
                Tuple.Create(Constants.DefaultConnectionStringName, "AccountEndpoint=https://defaultUri;AccountKey=c29tZV9rZXk=;"),
            });

            var factory = new DefaultCosmosDBServiceFactory(config, Mock.Of<AzureComponentFactory>());
            var options = new CosmosClientOptions()
            {
                ApplicationName = Guid.NewGuid().ToString()
            };

            // Act
            var client = factory.CreateService(null, options);

            // Assert
            Assert.NotNull(client);
            Assert.True(client.Endpoint.ToString().Contains("default"));
            Assert.Equal(options.ApplicationName, client.ClientOptions.ApplicationName);
        }

        [Fact]
        public void UsesConfig()
        {
            // Arrange
            var config = CosmosDBTestUtility.BuildConfiguration(new List<Tuple<string, string>>()
            {
                Tuple.Create(Constants.DefaultConnectionStringName, "AccountEndpoint=https://defaultUri;AccountKey=c29tZV9rZXk=;"),
                Tuple.Create("Attribute", "AccountEndpoint=https://attributeUri;AccountKey=c29tZV9rZXk=;")
            });

            var factory = new DefaultCosmosDBServiceFactory(config, Mock.Of<AzureComponentFactory>());

            // Act
            var client = factory.CreateService("Attribute", new CosmosClientOptions());

            // Assert
            Assert.NotNull(client);
            Assert.True(client.Endpoint.ToString().Contains("attribute"));
        }

        [Fact]
        public void FailsIfNotExists()
        {
            // Arrange
            var config = CosmosDBTestUtility.BuildConfiguration(new List<Tuple<string, string>>()
            {
                Tuple.Create(Constants.DefaultConnectionStringName, "AccountEndpoint=https://defaultUri;AccountKey=c29tZV9rZXk=;")
            });

            var factory = new DefaultCosmosDBServiceFactory(config, Mock.Of<AzureComponentFactory>());

            // Assert
            Assert.Throws<InvalidOperationException>(() => factory.CreateService("Attribute", new CosmosClientOptions()));
        }

        [Fact]
        public void CreatesCredentials_NoEndpoint()
        {
            // Arrange
            var myConfiguration = new Dictionary<string, string>
            {
                { "Credentials:tenantId", "ConfigurationTenant" },
                { "Credentials:clientId", "ConfigurationClientId" },
                { "Credentials:clientSecret", "ConfigurationSecret" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(myConfiguration)
                .Build();

            var factory = new DefaultCosmosDBServiceFactory(configuration, Mock.Of<AzureComponentFactory>());

            // Act
            Assert.Throws<InvalidOperationException>(() => factory.CreateService("Credentials", new CosmosClientOptions()));
        }

        [Fact]
        public void CreatesCredentials()
        {
            // Arrange
            var myConfiguration = new Dictionary<string, string>
            {
                { "Credentials:accountEndpoint", "http://someEndpoint" },
                { "Credentials:tenantId", "ConfigurationTenant" },
                { "Credentials:clientId", "ConfigurationClientId" },
                { "Credentials:clientSecret", "ConfigurationSecret" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(myConfiguration)
                .Build();

            var componentFactoryMock = new Mock<AzureComponentFactory>();
            componentFactoryMock.Setup(f => f.CreateTokenCredential(It.Is<IConfigurationSection>(section => section.Key == "Credentials")))
                .Returns(Mock.Of<TokenCredential>());

            var factory = new DefaultCosmosDBServiceFactory(configuration, componentFactoryMock.Object);

            // Act
            var client = factory.CreateService("Credentials", new CosmosClientOptions());

            // Assert
            Assert.NotNull(client);
            Assert.True(client.Endpoint.ToString().Contains("someendpoint"));
        }
    }
}
