// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.Azure.WebJobs.Extensions.MobileApps.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.MobileServices;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.MobileApps
{
    public class MobileAppsConfigurationTests
    {
        private Uri _configUri = new Uri("https://config/");

        [Fact]
        public void ResolveApiKey_ReturnsNull_IfAttributeEmpty()
        {
            // Arrange
            var config = CreateConfigProvider("Config", _configUri);

            // Act
            var apiKey = config.ResolveApiKey(string.Empty);

            // Assert            
            Assert.Null(apiKey);
        }

        [Fact]
        public void ResolveApiKey_UsesAttribute_First()
        {
            // Arrange
            var config = CreateConfigProvider("Config", _configUri);

            // Act
            var apiKey = config.ResolveApiKey("Attribute");

            // Assert            
            Assert.Equal("ResolvedAttribute", apiKey);
        }

        [Fact]
        public void ResolveApiKey_UsesConfig_Second()
        {
            // Arrange
            var config = CreateConfigProvider("Config", _configUri);

            // Act
            var apiKey = config.ResolveApiKey(null);

            // Assert            
            Assert.Equal("Config", apiKey);
        }

        [Fact]
        public void ResolveApiKey_UsesDefault_Last()
        {
            // Arrange
            var config = CreateConfigProvider(null, _configUri);

            // Act
            var apiKey = config.ResolveApiKey(null);

            // Assert            
            Assert.Equal("Default", apiKey);
        }

        [Fact]
        public void ResolveUri_UsesAttribute_First()
        {
            // Arrange
            var config = CreateConfigProvider("Config", _configUri);

            // Act
            var uri = config.ResolveMobileAppUri("https://attribute/");

            // Assert            
            Assert.Equal("https://attribute/", uri.ToString());
        }

        [Fact]
        public void ResolveUri_UsesConfig_Second()
        {
            // Arrange
            var config = CreateConfigProvider("Config", _configUri);

            // Act
            var uri = config.ResolveMobileAppUri(null);

            // Assert            
            Assert.Equal("https://config/", uri.ToString());
        }

        [Fact]
        public void ResolveUri_UsesDefault_Last()
        {
            // Arrange
            var config = CreateConfigProvider("Config", null);

            // Act
            var uri = config.ResolveMobileAppUri(null);

            // Assert            
            Assert.Equal("https://default/", uri.ToString());
        }

        [Fact]
        public void GetClient_Caches()
        {
            var mockFactory = new Mock<IMobileServiceClientFactory>(MockBehavior.Strict);
            Uri uri1 = new Uri("https://someuri1");
            Uri uri2 = new Uri("https://someuri2");

            mockFactory
                .Setup(f => f.CreateClient(It.IsAny<Uri>(), null))
                .Returns<Uri, HttpMessageHandler[]>((uri, handlers) => new MobileServiceClient(uri, handlers));

            var configProvider = CreateConfigProvider("Default", _configUri, mockFactory.Object);

            var client1 = configProvider.GetClient(uri1, null);
            var client2 = configProvider.GetClient(uri1, null);
            var client3 = configProvider.GetClient(uri2, null);
            var client4 = configProvider.GetClient(uri1, null);

            Assert.Same(client1, client2);
            Assert.Same(client2, client4);
            Assert.NotSame(client1, client3);
            mockFactory.Verify(f => f.CreateClient(uri1, null), Times.Once);
            mockFactory.Verify(f => f.CreateClient(uri2, null), Times.Once);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("my_api_key", true)]
        public async Task CreateMobileServiceClient_AddsHandler(string apiKey, bool expectHeader)
        {
            // Arrange
            var handler = new TestHandler();
            var factory = new TestMobileServiceClientFactory(handler);
            var client = MobileAppsExtensionConfigProvider.CreateMobileServiceClient(factory, new Uri("https://someuri/"), apiKey);
            var table = client.GetTable("FakeTable");

            // Act
            await table.ReadAsync(string.Empty);

            // Assert
            IEnumerable<string> values = null;
            bool foundHeader = handler.IssuedRequest.Headers.TryGetValues(MobileServiceApiKeyHandler.ZumoApiKeyHeaderName, out values);

            Assert.Equal(expectHeader, foundHeader);
            if (expectHeader)
            {
                Assert.Equal("my_api_key", values.Single());
            }
        }

        [Fact]
        public void CreateCacheKey_SucceedsWith_NullApiKey()
        {
            // Arrange
            Uri uri = new Uri("http://someuri");

            // Act
            string key = MobileAppsExtensionConfigProvider.GetCacheKey(uri, null);

            // Assert
            Assert.Equal(uri.ToString() + ";", key);
        }

        [Fact]
        public void CreateCacheKey_MatchesUri_DifferentCasing()
        {
            // Arrange
            Uri uri1 = new Uri("http://someuri");
            Uri uri2 = new Uri("http://SOMEURI");

            string apiKey = "api_key";

            // Act
            string key1 = MobileAppsExtensionConfigProvider.GetCacheKey(uri1, apiKey);
            string key2 = MobileAppsExtensionConfigProvider.GetCacheKey(uri2, apiKey);

            // Assert
            Assert.Equal(key1, key2);
        }

        [Fact]
        public void BindForQuery_ReturnsCorrectType()
        {
            var attribute = new MobileTableAttribute();
            var configProvider = CreateConfigProvider("Default", new Uri("https://someuri/"));

            var queryBuilder = new MobileTableQueryBuilder<TodoItem>(configProvider);
            var query = queryBuilder.Convert(attribute);

            Assert.True(typeof(IMobileServiceTableQuery<TodoItem>).IsAssignableFrom(query.GetType()));
        }

        [Fact]
        public void BindForQuery_WithTableName_ReturnsCorrectType()
        {
            var attribute = new MobileTableAttribute
            {
                TableName = "SomeOtherTable"
            };
            var configProvider = CreateConfigProvider("Default", new Uri("https://someuri/"));

            var queryBuilder = new MobileTableQueryBuilder<TodoItem>(configProvider);
            var query = queryBuilder.Convert(attribute);

            Assert.NotNull(query);
            Assert.Equal("SomeOtherTable", query.Table.TableName);
        }

        [Fact]
        public void BindForTable_JObject_ReturnsCorrectTable()
        {
            // Arrange
            var attribute = new MobileTableAttribute
            {
                TableName = "TodoItem"
            };
            var configProvider = CreateConfigProvider("Default", new Uri("https://someuri/"));

            // Act
            var tableBuilder = new MobileTableJObjectTableBuilder(configProvider);
            var table = tableBuilder.Convert(attribute);

            // Assert
            Assert.NotNull(table);
            Assert.Equal("TodoItem", table.TableName);
        }

        [Fact]
        public void BindForTable_Poco_ReturnsCorrectTable()
        {
            // Arrange
            var attribute = new MobileTableAttribute();
            var configProvider = CreateConfigProvider("Default", new Uri("https://someuri/"));

            // Act
            var tableBuilder = new MobileTablePocoTableBuilder<TodoItem>(configProvider);
            var table = tableBuilder.Convert(attribute);

            // Assert
            Assert.NotNull(table);
            Assert.Equal("TodoItem", table.TableName);
        }

        [Fact]
        public void GetValue_PocoWithTableName_ReturnsCorrectTable()
        {
            // Arrange
            var attribute = new MobileTableAttribute
            {
                TableName = "SomeOtherTable"
            };
            var configProvider = CreateConfigProvider("Default", new Uri("https://someuri/"));

            // Act
            var tableBuilder = new MobileTablePocoTableBuilder<TodoItem>(configProvider);
            var table = tableBuilder.Convert(attribute);

            // Assert
            Assert.NotNull(table);
            Assert.Equal("SomeOtherTable", table.TableName);
        }

        private MobileAppsExtensionConfigProvider CreateConfigProvider(string configApiKey, Uri configMobileAppUri, IMobileServiceClientFactory clientFactory = null)
        {
            var options = new MobileAppsOptions
            {
                ApiKey = configApiKey,
                MobileAppUri = configMobileAppUri
            };

            var nameResolver = new TestNameResolver();
            nameResolver.Values[MobileAppsExtensionConfigProvider.AzureWebJobsMobileAppApiKeyName] = "Default";
            nameResolver.Values[MobileAppsExtensionConfigProvider.AzureWebJobsMobileAppUriName] = "https://default";
            nameResolver.Values["Attribute"] = "ResolvedAttribute";

            clientFactory = clientFactory ?? new DefaultMobileServiceClientFactory();
            var configProvider = new MobileAppsExtensionConfigProvider(new OptionsWrapper<MobileAppsOptions>(options), clientFactory, nameResolver);
            var context = TestHelpers.CreateExtensionConfigContext(nameResolver);
            configProvider.Initialize(context);

            return configProvider;
        }
    }
}
