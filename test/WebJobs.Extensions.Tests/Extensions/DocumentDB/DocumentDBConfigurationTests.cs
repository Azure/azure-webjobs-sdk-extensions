// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBConfigurationTests
    {
        private const string ConnectionStringKey = DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName;
        private const string AppSettingKey = ConnectionStringKey + "_appsetting";
        private const string EnvironmentKey = ConnectionStringKey + "_environment";
        private const string NeitherKey = ConnectionStringKey + "_neither";

        [Fact]
        public async Task Configuration_Caches_Clients()
        {
            // Arrange            
            var config = new DocumentDBConfiguration
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=some_key",                
            };
            var attribute = new DocumentDBAttribute();

            // Act
            var client = config.BindForClient(attribute);
            var context = config.BindForOutput(attribute, typeof(Item), null);
            var binder = await config.BindForItemAsync(attribute, typeof(Item), null);

            // Assert
            Assert.Equal(1, config.ClientCache.Count);
        }

        [Fact]
        public void Resolve_UsesConnectionString_First()
        {
            // Arrange
            SetEnvironment(ConnectionStringKey);

            // Act
            var connString = DocumentDBConfiguration.GetSettingFromConfigOrEnvironment(ConnectionStringKey);

            // Assert            
            Assert.Equal("AccountEndpoint=https://fromconnstrings;AccountKey=some_key", connString);

            ClearEnvironment();
        }

        [Fact]
        public void Resolve_UsesAppSetting_Second()
        {
            // Arrange
            SetEnvironment(AppSettingKey);

            // Act
            var connString = DocumentDBConfiguration.GetSettingFromConfigOrEnvironment(AppSettingKey);

            // Assert
            Assert.Equal("AccountEndpoint=https://fromappsettings2;AccountKey=some_key", connString);

            ClearEnvironment();
        }

        [Fact]
        public void Resolve_UsesEnvironment_Third()
        {
            // Arrange
            SetEnvironment(EnvironmentKey);

            // Act
            var connString = DocumentDBConfiguration.GetSettingFromConfigOrEnvironment(EnvironmentKey);

            // Assert
            Assert.Equal("https://fromenvironment/", connString);

            ClearEnvironment();
        }

        [Fact]
        public void Resolve_FallsBackToNull()
        {
            // Arrange
            ClearEnvironment();

            // Act
            var connString = DocumentDBConfiguration.GetSettingFromConfigOrEnvironment(NeitherKey);

            // Assert            
            Assert.Null(connString);
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
            config.CreateContext(attribute, new TestTraceWriter());

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
            var context = config.CreateContext(attribute, new TestTraceWriter());

            // Assert
            Assert.Equal(DocumentDBContext.DefaultMaxThrottleRetries, context.MaxThrottleRetries);
        }

        private static void SetEnvironment(string key)
        {
            Environment.SetEnvironmentVariable(key, "https://fromenvironment/");
        }

        private static void ClearEnvironment()
        {
            Environment.SetEnvironmentVariable(ConnectionStringKey, null);
            Environment.SetEnvironmentVariable(EnvironmentKey, null);
            Environment.SetEnvironmentVariable(NeitherKey, null);
        }       
    }
}
