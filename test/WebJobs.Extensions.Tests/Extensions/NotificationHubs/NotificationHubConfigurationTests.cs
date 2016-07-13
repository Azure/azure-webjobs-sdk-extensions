// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.NotificationHubs;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Config;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.NotificationHubs
{
    public class NotificationHubConfigurationTests
    {
        private readonly string _defaultConnectionString;

        public NotificationHubConfigurationTests()
        {
            var nameResolver = new DefaultNameResolver();
            _defaultConnectionString = nameResolver.Resolve(NotificationHubsConfiguration.NotificationHubConnectionStringName);
        }

        [Fact]
        public void Resolve_UsesAttribute_First()
        {
            var config = InitializeConfig("Config");

            // Act
            var connString = config.ResolveConnectionString("Attribute");

            // Assert
            Assert.Equal("Attribute", connString);
        }

        [Fact]
        public void Resolve_UsesConfig_Second()
        {
            var config = InitializeConfig();
            config.ConnectionString = "Config";

            // Act
            var connString = config.ResolveConnectionString(null);

            // Assert
            Assert.Equal("Config", connString);
        }

        [Fact]
        public void Resolve_UsesDefault_Last()
        {
            var config = InitializeConfig();

            // Act
            var connString = config.ResolveConnectionString(null);

            // Assert
            Assert.Equal(_defaultConnectionString, connString);
        }

        private NotificationHubsConfiguration InitializeConfig(string configConnectionString = null)
        {
            var config = new NotificationHubsConfiguration();

            if (configConnectionString != null)
            {
                config.ConnectionString = configConnectionString;
            }

            var nameResolver = new TestNameResolver();
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
