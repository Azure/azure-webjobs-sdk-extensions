// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Microsoft.Azure.WebJobs.Host.Config;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBConfigurationTests
    {
        public static IEnumerable<object[]> ValidInputBindings
        {
            get
            {
                return new[]
                {
                    new object[] { new DocumentDBAttribute(), typeof(IEnumerable<JObject>) },
                    new object[] { new DocumentDBAttribute { Id = "SomeId" }, typeof(JObject) },
                    new object[] { new DocumentDBAttribute { SqlQuery="Some Query" }, typeof(IEnumerable<JObject>) }
                };
            }
        }

        public static IEnumerable<object[]> InvalidInputBindings
        {
            get
            {
                return new[]
                {
                    new object[] { new DocumentDBAttribute { Id = "SomeId", SqlQuery = "Some Query" }, typeof(JObject) },
                    new object[] { new DocumentDBAttribute { Id = "SomeId" }, typeof(IEnumerable<JObject>) },
                    new object[] { new DocumentDBAttribute { SqlQuery="Some Query" }, typeof(JObject) },
                    new object[] { new DocumentDBAttribute { SqlQuery="Some Query" }, typeof(IList<JObject>) },
                    new object[] { new DocumentDBAttribute { SqlQuery="Some Query" }, typeof(List<JObject>) },
                };
            }
        }

        [Fact]
        public async Task Configuration_Caches_Clients()
        {
            // Arrange            
            var config = new DocumentDBConfiguration
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=c29tZV9rZXk=;",
            };
            var attribute = new DocumentDBAttribute { Id = "abcdef" };

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

        [Theory]
        [InlineData(typeof(IEnumerable<string>), true)]
        [InlineData(typeof(IEnumerable<Document>), true)]
        [InlineData(typeof(IEnumerable<JObject>), true)]
        [InlineData(typeof(JArray), false)]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(List<Document>), false)]
        public void TryGetEnumerableType(Type type, bool expectedResult)
        {
            bool actualResult = DocumentDBConfiguration.IsSupportedEnumerable(type);
            Assert.Equal(expectedResult, actualResult);
        }

        [Theory]
        [MemberData("ValidInputBindings")]
        public void ValidateInputBindings_Succeeds_WithValidBindings(DocumentDBAttribute attribute, Type parameterType)
        {
            DocumentDBConfiguration.ValidateInputBinding(attribute, parameterType);
        }

        [Theory]
        [MemberData("InvalidInputBindings")]
        public void ValidateInputBindings_Throws_WithInvalidBindings(DocumentDBAttribute attribute, Type parameterType)
        {
            Assert.Throws<InvalidOperationException>(() => DocumentDBConfiguration.ValidateInputBinding(attribute, parameterType));
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
