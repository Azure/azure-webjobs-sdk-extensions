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

        private NotificationHubsConfiguration InitializeConfig(string defaultConnStr)
        {
            var config = new NotificationHubsConfiguration();

            var nameResolver = new TestNameResolver();
            nameResolver.Values[NotificationHubsConfiguration.NotificationHubConnectionStringName] = defaultConnStr;

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
