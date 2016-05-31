// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps;
using Microsoft.WindowsAzure.MobileServices;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.MobileApps
{
    public class MobileAppsConfigurationTests
    {
        private const string AppSettingKey = MobileAppsConfiguration.AzureWebJobsMobileAppUriName;
        private const string EnvironmentKey = AppSettingKey + "_environment";
        private const string NeitherKey = AppSettingKey + "_neither";

        [Fact]
        public void Resolve_UsesAppSettings_First()
        {
            // Arrange
            SetEnvironment(AppSettingKey);

            // Act
            var mobileAppUri = MobileAppsConfiguration.GetSettingFromConfigOrEnvironment(AppSettingKey);

            // Assert            
            Assert.Equal("https://fromappsettings/", mobileAppUri.ToString());

            ClearEnvironment();
        }

        [Fact]
        public void Resolve_UsesEnvironment_Second()
        {
            // Arrange
            SetEnvironment(EnvironmentKey);

            // Act
            var mobileAppUri = MobileAppsConfiguration.GetSettingFromConfigOrEnvironment(EnvironmentKey);

            // Assert
            Assert.Equal("https://fromenvironment/", mobileAppUri.ToString());

            ClearEnvironment();
        }

        [Fact]
        public void Resolve_FallsBackToNull()
        {
            // Arrange
            ClearEnvironment();

            // Act
            var mobileAppUri = MobileAppsConfiguration.GetSettingFromConfigOrEnvironment(NeitherKey);

            // Assert            
            Assert.Null(mobileAppUri);
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

            var config = new MobileAppsConfiguration
            {
                ClientFactory = mockFactory.Object
            };

            var client1 = config.GetClient(uri1, null);
            var client2 = config.GetClient(uri1, null);
            var client3 = config.GetClient(uri2, null);
            var client4 = config.GetClient(uri1, null);

            Assert.Same(client1, client2);
            Assert.Same(client2, client4);
            Assert.NotSame(client1, client3);
            mockFactory.Verify(f => f.CreateClient(uri1, null), Times.Once);
            mockFactory.Verify(f => f.CreateClient(uri2, null), Times.Once);
        }

        [Theory]
        [InlineData("MyMobileAppUri", "https://configUri", "https://attruri/")]
        [InlineData(null, "https://configUri", "https://configuri/")]
        [InlineData("", "https://configUri", "https://configuri/")]
        public void CreateContext_AttributeUri_Wins(string attributeUriString, string configUriString, string expectedUriString)
        {
            // Arrange
            var attribute = new MobileTableAttribute
            {
                MobileAppUriSetting = attributeUriString
            };

            var mockFactory = new Mock<IMobileServiceClientFactory>();
            mockFactory
                .Setup(f => f.CreateClient(new Uri(expectedUriString), null))
                .Returns<IMobileServiceClient>(null);

            var config = new MobileAppsConfiguration
            {
                MobileAppUri = new Uri(configUriString),
                ClientFactory = mockFactory.Object
            };

            // Act
            config.CreateContext(attribute);

            // Assert
            mockFactory.VerifyAll();
        }

        [Theory]
        [InlineData("MyMobileAppKey", "config_key", "attr_key")]
        [InlineData(null, "config_key", "config_key")]
        [InlineData("", "config_key", null)]
        public void CreateContext_AttributeKey_Wins(string attributeKey, string configKey, string expectedKey)
        {
            // Arrange
            var attribute = new MobileTableAttribute
            {
                ApiKeySetting = attributeKey
            };

            var handler = new TestHandler();
            var config = new MobileAppsConfiguration
            {
                MobileAppUri = new Uri("https://someuri"),
                ApiKey = configKey,
                ClientFactory = new TestMobileServiceClientFactory(handler)
            };

            // Act
            var context = config.CreateContext(attribute);

            // Assert
            // Issue a request to check the header that's being sent.
            context.Client.GetTable("Test").LookupAsync("123");

            IEnumerable<string> values = null;
            string actualKey = null;
            if (handler.IssuedRequest.Headers.TryGetValues("ZUMO-API-KEY", out values))
            {
                actualKey = values.Single();
            }

            Assert.Equal(expectedKey, actualKey);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("my_api_key", true)]
        public async Task CreateMobileServiceClient_AddsHandler(string apiKey, bool expectHeader)
        {
            // Arrange
            var handler = new TestHandler();
            var factory = new TestMobileServiceClientFactory(handler);
            var client = MobileAppsConfiguration.CreateMobileServiceClient(factory, new Uri("https://someuri/"), apiKey);
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
            string key = MobileAppsConfiguration.GetCacheKey(uri, null);

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
            string key1 = MobileAppsConfiguration.GetCacheKey(uri1, apiKey);
            string key2 = MobileAppsConfiguration.GetCacheKey(uri2, apiKey);

            // Assert
            Assert.Equal(key1, key2);
        }

        [Fact]
        public async Task BindForQuery_ReturnsCorrectType()
        {
            var attribute = new MobileTableAttribute();
            var config = new MobileAppsConfiguration();

            var query = await config.BindForQueryAsync(attribute, typeof(IMobileServiceTableQuery<TodoItem>));

            Assert.True(typeof(IMobileServiceTableQuery<TodoItem>).IsAssignableFrom(query.GetType()));
        }

        [Fact]
        public async Task BindForQuery_WithTableName_ReturnsCorrectType()
        {
            var attribute = new MobileTableAttribute
            {
                TableName = "SomeOtherTable"
            };

            var config = new MobileAppsConfiguration();

            var query = await config.BindForQueryAsync(attribute, typeof(IMobileServiceTableQuery<TodoItem>)) as IMobileServiceTableQuery<TodoItem>;

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
            var config = new MobileAppsConfiguration();

            // Act
            var table = config.BindForTable(attribute);

            // Assert
            Assert.NotNull(table);
            Assert.Equal("TodoItem", table.TableName);
        }

        [Fact]
        public async Task BindForTable_Poco_ReturnsCorrectTable()
        {
            // Arrange
            var attribute = new MobileTableAttribute();
            var config = new MobileAppsConfiguration();

            // Act
            var table = await config.BindForTableAsync(attribute, typeof(IMobileServiceTable<TodoItem>)) as IMobileServiceTable<TodoItem>;

            // Assert
            Assert.NotNull(table);
            Assert.Equal("TodoItem", table.TableName);
        }

        [Fact]
        public async Task GetValue_PocoWithTableName_ReturnsCorrectTable()
        {
            // Arrange
            var attribute = new MobileTableAttribute
            {
                TableName = "SomeOtherTable"
            };
            var config = new MobileAppsConfiguration();

            // Act
            var table = await config.BindForTableAsync(attribute, typeof(IMobileServiceTable<TodoItem>)) as IMobileServiceTable<TodoItem>;

            // Assert
            Assert.NotNull(table);
            Assert.Equal("SomeOtherTable", table.TableName);
        }

        private static void SetEnvironment(string key)
        {
            Environment.SetEnvironmentVariable(key, "https://fromenvironment/");
        }

        private static void ClearEnvironment()
        {
            Environment.SetEnvironmentVariable(AppSettingKey, null);
            Environment.SetEnvironmentVariable(EnvironmentKey, null);
            Environment.SetEnvironmentVariable(NeitherKey, null);
        }
    }
}
