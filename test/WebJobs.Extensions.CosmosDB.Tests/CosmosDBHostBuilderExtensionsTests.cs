// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBHostBuilderExtensionsTests
    {
        [Fact]
        public void ConfigurationBindsToOptions()
        {
            IHost host = new HostBuilder()
                 .ConfigureAppConfiguration(c =>
                 {
                     c.Sources.Clear();

                     var source = new MemoryConfigurationSource
                     {
                         InitialData = new Dictionary<string, string>
                        {
                            { "AzureWebJobs:extensions:cosmosDB:ConnectionMode", "Direct" }
                        }
                     };

                     c.Add(source);
                 })
                 .ConfigureWebJobs(builder =>
                 {
                     builder.AddCosmosDB();
                 })
                .Build();

            var options = host.Services.GetService<IOptions<CosmosDBOptions>>().Value;

            Assert.Equal(ConnectionMode.Direct, options.ConnectionMode);
        }

        [Fact]
        public void ConfigurationBindsToOptions_WithConfigureServices()
        {
            IHost host = new HostBuilder()
                 .ConfigureWebJobs(builder =>
                 {
                     builder.AddCosmosDB();
                 })
                .ConfigureServices(s =>
                {
                    // Verifies that you can modify the bound options
                    s.Configure<CosmosDBOptions>(o => o.ConnectionMode = ConnectionMode.Direct);
                })
                .Build();

            var options = host.Services.GetService<IOptions<CosmosDBOptions>>().Value;

            Assert.Equal(ConnectionMode.Direct, options.ConnectionMode);
        }

        [Fact]
        public void ConfigurationBindsToOptions_WithSerializer()
        {
            CustomFactory customFactory = new CustomFactory();
            IHost host = new HostBuilder()
                .ConfigureAppConfiguration(c =>
                {
                    c.Sources.Clear();
                    c.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        { Constants.DefaultConnectionStringName, "AccountEndpoint=https://defaultUri;AccountKey=c29tZV9rZXk=;" }
                    });
                })
                .ConfigureWebJobs(builder =>
                {
                    builder.AddCosmosDB();
                })
                .ConfigureServices(s =>
                {
                    s.AddSingleton<ICosmosDBSerializerFactory>(customFactory);
                    s.TryAddSingleton(Mock.Of<AzureComponentFactory>());
                })
                .Build();

            var extensionConfig = host.Services.GetServices<IExtensionConfigProvider>().Single();
            Assert.NotNull(extensionConfig);
            Assert.IsType<CosmosDBExtensionConfigProvider>(extensionConfig);

            CosmosDBExtensionConfigProvider cosmosDBExtensionConfigProvider = (CosmosDBExtensionConfigProvider)extensionConfig;
            CosmosClient dummyClient = cosmosDBExtensionConfigProvider.GetService(Constants.DefaultConnectionStringName);
            Assert.True(customFactory.CreateWasCalled);
        }

        [Fact]
        public void ConfigurationBindsToOptions_WithSerializer_Before()
        {
            CustomFactory customFactory = new CustomFactory();
            IHost host = new HostBuilder()
                .ConfigureAppConfiguration(c =>
                {
                    c.Sources.Clear();
                    c.AddInMemoryCollection(new Dictionary<string, string>
                    {
                { Constants.DefaultConnectionStringName, "AccountEndpoint=https://defaultUri;AccountKey=c29tZV9rZXk=;" }
                    });
                })
                .ConfigureServices(s =>
                {
                    s.AddSingleton<ICosmosDBSerializerFactory>(customFactory);
                    s.TryAddSingleton(Mock.Of<AzureComponentFactory>());
                })
                .ConfigureWebJobs(builder =>
                {
                    builder.AddCosmosDB();
                })                
                .Build();

            var extensionConfig = host.Services.GetServices<IExtensionConfigProvider>().Single();
            Assert.NotNull(extensionConfig);
            Assert.IsType<CosmosDBExtensionConfigProvider>(extensionConfig);

            CosmosDBExtensionConfigProvider cosmosDBExtensionConfigProvider = (CosmosDBExtensionConfigProvider)extensionConfig;
            CosmosClient dummyClient = cosmosDBExtensionConfigProvider.GetService(Constants.DefaultConnectionStringName);
            Assert.True(customFactory.CreateWasCalled);
        }

        private class CustomFactory : ICosmosDBSerializerFactory
        {
            public bool CreateWasCalled { get; private set; } = false;

            public CosmosSerializer CreateSerializer()
            {
                this.CreateWasCalled = true;
                return new CustomSerializer();
            }
        }

        private class CustomSerializer : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                throw new NotImplementedException();
            }

            public override Stream ToStream<T>(T input)
            {
                throw new NotImplementedException();
            }
        }
    }
}
