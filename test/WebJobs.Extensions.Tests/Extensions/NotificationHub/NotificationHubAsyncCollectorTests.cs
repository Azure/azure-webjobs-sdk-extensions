// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Extensions.NotificationHub;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.NotificationHub
{
    public class NotificationHubAsyncCollectorTests
    {
        [Fact]
        public async Task AddAsync_SendNotification_TagExpression_Null()
        {
            var notification = GetTemplateNotification();

            // Arrange
            var mockNhClientService = new Mock<INotificationHubClientService>(MockBehavior.Strict);
            mockNhClientService.Setup(x => x.SendNotificationAsync(notification, null))
                  .Returns(Task.FromResult(new NotificationOutcome()));

            IAsyncCollector<Notification> collector = new NotificationHubAsyncCollector(mockNhClientService.Object, null);

            // Act
            await collector.AddAsync(notification);

            // Assert
            mockNhClientService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_SendNotification_TagExpression_Valid()
        {
            var notification = GetTemplateNotification();
            // Arrange
            var mockNhClientService = new Mock<INotificationHubClientService>(MockBehavior.Strict);

            mockNhClientService.Setup(x => x.SendNotificationAsync(notification, "foo||bar"))
                    .Returns(Task.FromResult(new NotificationOutcome()));

            IAsyncCollector<Notification> collector = new NotificationHubAsyncCollector(mockNhClientService.Object, "foo||bar");

            // Act
            await collector.AddAsync(notification);

            // Assert
            mockNhClientService.VerifyAll();
        }

        private static Notification GetTemplateNotification()
        {
            Dictionary<string, string> templateProperties = new Dictionary<string, string>();
            templateProperties["message"] = "Hello";
            return new TemplateNotification(templateProperties);
        }
    }
}
