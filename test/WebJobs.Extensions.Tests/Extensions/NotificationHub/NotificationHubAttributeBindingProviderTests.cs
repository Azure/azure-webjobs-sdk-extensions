﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Extensions.NotificationHub;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.NotificationHub
{
    public class NotificationHubAttributeBindingProviderTests
    {
        public static IEnumerable<object[]> ValidParameters
        {
            get
            {
                return GetValidOutputParameters().Select(p => new object[] { p });
            }
        }

        public static IEnumerable<object[]> InvalidParameters
        {
            get
            {
                return GetInvalidOutputParameters().Select(p => new object[] { p });
            }
        }

        [Theory]
        [MemberData("ValidParameters")]
        public async Task TryCreateAsync_CreatesBinding_ForValidParameters(ParameterInfo parameter)
        {
            // Act
            var binding = await CreateProviderAndTryCreateAsync(parameter);

            // Assert
            Assert.NotNull(binding);
        }

        [Theory]
        [MemberData("InvalidParameters")]
        public async Task TryCreateAsync_ReturnsNull_ForInvalidParameters(ParameterInfo parameter)
        {
            // Arrange
            Type expectedExceptionType = typeof(InvalidOperationException);

            // Act
            var exception = await Assert.ThrowsAnyAsync<Exception>(() => CreateProviderAndTryCreateAsync(parameter));

            // Assert
            Assert.Equal(expectedExceptionType, exception.GetType());
        }

        private static Task<IBinding> CreateProviderAndTryCreateAsync(ParameterInfo parameter)
        {
            var jobConfig = new JobHostConfiguration();
            var config = new NotificationHubConfiguration()
            {
                ConnectionString = "sb://testconnetionstring",
                HubName = "testHub"
            };
            var context = new BindingProviderContext(parameter, null, CancellationToken.None);
            ExtensionConfigContext extensionsConfigContext = new ExtensionConfigContext();
            extensionsConfigContext.Config = jobConfig;
            config.Initialize(extensionsConfigContext);
            var nameResolver = new TestNameResolver();
            var converterManager = jobConfig.GetService<IConverterManager>();
            var provider = new NotificationHubAttributeBindingProvider(nameResolver, converterManager, config);
            
            return provider.TryCreateAsync(context);
        }

        private static IEnumerable<ParameterInfo> GetValidOutputParameters()
        {
            return typeof(NotificationHubAttributeBindingProviderTests)
                .GetMethod("OutputParameters", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters();
        }

        private static IEnumerable<ParameterInfo> GetInvalidOutputParameters()
        {
            return typeof(NotificationHubAttributeBindingProviderTests)
                .GetMethod("InvalidOutputParameters", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters();
        }

        private void OutputParameters(
            [NotificationHub] out Notification notification,
            [NotificationHub] out TemplateNotification templateNotification,
            [NotificationHub] out string templateProperties,
            [NotificationHub] out Notification[] notificationsArray,
            [NotificationHub] out IDictionary<string, string> templatePropertiesDictionary,
            [NotificationHub] IAsyncCollector<TemplateNotification> asyncCollector,
            [NotificationHub] IAsyncCollector<string> asyncCollectorString,
            [NotificationHub] ICollector<Notification> collector,
            [NotificationHub] ICollector<string> collectorString)
        {
            notification = null;
            templateNotification = null;
            templateProperties = null;
            templatePropertiesDictionary = null;
            notificationsArray = null;
        }

        private void InvalidOutputParameters(
            [NotificationHub] Notification notification,
            [NotificationHub] TemplateNotification templateNotification)
        {
        }
    }
}
