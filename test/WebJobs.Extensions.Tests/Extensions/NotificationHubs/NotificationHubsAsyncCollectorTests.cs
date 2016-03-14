// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Extensions.NotificationHubs;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.NotificationHubs
{
    public class NotificationHubsAsyncCollectorTests
    {
        [Fact]
        public async Task AddAsync_SendNotification_TagExpression_Null()
        {
            // Arrange
            var mockNhClientService = new Mock<INotificationHubClientService>(MockBehavior.Strict);
            mockNhClientService.Setup(x => x.SendNotificationAsync(GetTemplateNotification(), null))
                  .Returns(Task.FromResult(new NotificationOutcome()));

            IAsyncCollector<Notification> collector = new NotificationHubsAsyncCollector(mockNhClientService.Object, null);

            // Act
            await collector.AddAsync(GetTemplateNotification());

            // Assert
            mockNhClientService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_SendNotification_TagExpression_Valid()
        {
            // Arrange
            var mockNhClientService = new Mock<INotificationHubClientService>(MockBehavior.Strict);
            mockNhClientService.Setup(x => x.SendNotificationAsync(GetTemplateNotification(), "foo||bar"))
                    .Returns(Task.FromResult(new NotificationOutcome()));

            IAsyncCollector<Notification> collector = new NotificationHubsAsyncCollector(mockNhClientService.Object, "foo||bar");

            // Act
            await collector.AddAsync(GetTemplateNotification());

            // Assert
            mockNhClientService.VerifyAll();
        }
        private static Notification GetTemplateNotification()
        {
            Dictionary<string, string> templateProperties = new Dictionary<string, string>();
            templateProperties["message"] = "bar";
            Notification testTemplateNotification = new TemplateNotification(templateProperties);
            return testTemplateNotification;
        }
    }
}
