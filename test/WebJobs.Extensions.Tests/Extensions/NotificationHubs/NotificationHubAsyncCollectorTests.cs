// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Extensions.NotificationHubs;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.NotificationHubs
{
    public class NotificationHubAsyncCollectorTests
    {
       public static string debugLogNoResults = "NotificationHubs Test Send\r\n" +
                    "  TrackingId = \r\n" +
                    "  State = Enqueued\r\n" +
                    "  Results (Success = 0, Failure = 0)\r\n";

        [Fact]
        public async Task AddAsync_SendNotification_TagExpression_Null()
        {
            var notification = GetTemplateNotification();

            // Arrange
            var mockNhClientService = new Mock<INotificationHubClientService>(MockBehavior.Strict);
            mockNhClientService.Setup(x => x.SendNotificationAsync(notification, null))
                  .Returns(Task.FromResult(new NotificationOutcome()));

            IAsyncCollector<Notification> collector = new NotificationHubAsyncCollector(mockNhClientService.Object, null, false, new TestTraceWriter(TraceLevel.Info));

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

            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Info);

            IAsyncCollector<Notification> collector = new NotificationHubAsyncCollector(mockNhClientService.Object, "foo||bar", false, trace);

            // Act
            await collector.AddAsync(notification);

            // Assert
            mockNhClientService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_SendNotification_EnableTestSend_Results_Null()
        {
            var notification = GetTemplateNotification();
            // Arrange
            var mockNhClientService = new Mock<INotificationHubClientService>(MockBehavior.Strict);

            mockNhClientService.Setup(x => x.SendNotificationAsync(notification, "foo||bar"))
                    .Returns(Task.FromResult(new NotificationOutcome()));

            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Info);

            IAsyncCollector<Notification> collector = new NotificationHubAsyncCollector(mockNhClientService.Object, "foo||bar", true, trace);

            // Act
            await collector.AddAsync(notification);

            // Assert
            Assert.Equal(1, trace.Events.Count);
            Assert.True(trace.Events[0].Message.Equals(debugLogNoResults));
        }

        [Fact]
        public async Task AddAsync_SendNotification_EnableTestSend_Results_NotNull()
        {
            var notification = GetTemplateNotification();
            RegistrationResult reg = new RegistrationResult
            {
                ApplicationPlatform = "Windows",
                Outcome = "Successfully sent Push notification",
                PnsHandle = "Some-GUID",
                RegistrationId = "Another-GUID"
            };

            var registrationList = new List<RegistrationResult>();
            registrationList.Add(reg);

            NotificationOutcome notificationOutcome = new NotificationOutcome
            {
                Results = registrationList,
            };
            string registrationOutcome = $"NotificationHubs Test Send\r\n" +
                   $"  TrackingId = {notificationOutcome.TrackingId}\r\n" +
                   $"  State = {notificationOutcome.State}\r\n" +
                   $"  Results (Success = {notificationOutcome.Success}, Failure = {notificationOutcome.Failure})\r\n"+
                   $"    ApplicationPlatform:{reg.ApplicationPlatform}, RegistrationId:{reg.RegistrationId}, Outcome:{reg.Outcome}\r\n";
        
            // Arrange
            var mockNhClientService = new Mock<INotificationHubClientService>(MockBehavior.Strict);

            mockNhClientService.Setup(x => x.SendNotificationAsync(notification, "foo||bar"))
                    .Returns(Task.FromResult(notificationOutcome));

            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Info);

            IAsyncCollector<Notification> collector = new NotificationHubAsyncCollector(mockNhClientService.Object, "foo||bar", true, trace);

            // Act
            await collector.AddAsync(notification);

            // Assert
            Assert.Equal(1, trace.Events.Count);
            Assert.True(trace.Events[0].Message.Equals(registrationOutcome));

            mockNhClientService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_SendNotification_EnableTestSend_Results_Empty()
        {
            var notification = GetTemplateNotification();
            
            NotificationOutcome outcome = new NotificationOutcome
            {
                Results = new List<RegistrationResult>()
            };

            // Arrange
            var mockNhClientService = new Mock<INotificationHubClientService>(MockBehavior.Strict);

            mockNhClientService.Setup(x => x.SendNotificationAsync(notification, "foo||bar"))
                    .Returns(Task.FromResult(outcome));

            TestTraceWriter trace = new TestTraceWriter(TraceLevel.Info);

            IAsyncCollector<Notification> collector = new NotificationHubAsyncCollector(mockNhClientService.Object, "foo||bar", true, trace);

            // Act
            await collector.AddAsync(notification);

            // Assert
            Assert.Equal(1, trace.Events.Count);
            Assert.True(trace.Events[0].Message.Equals(debugLogNoResults));

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
