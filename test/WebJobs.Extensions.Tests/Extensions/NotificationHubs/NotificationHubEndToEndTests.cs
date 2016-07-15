// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.NotificationHubs;
using Microsoft.Azure.WebJobs.Extensions.NotificationHubs;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.NotificationHubs
{
    public class NotificationHubEndToEndTests
    {
        private const string AttributeConnStr = "Endpoint=sb://TestAttrNS.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=RandomKey";
        private const string AttributeHubName = "AttributeHubName";
        private const string ConfigConnStr = "Endpoint=sb://TestConfigNS.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=RandomKey";
        private const string DefaultConnStr = "Endpoint=sb://TestNS.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=RandomKey";
        private const string DefaultHubName = "TestHub";
        private const string MessagePropertiesJSON = "{\"message\":\"Hello\",\"location\":\"Redmond\"}";
        private const string UserIdTag = "myuserid123";
        private static Notification TestNotification = Converter.BuildTemplateNotificationFromJsonString(MessagePropertiesJSON);

        [Fact]
        public void OutputBindings()
        {
            //Arrage
            var serviceMock = new Mock<INotificationHubClientService>(MockBehavior.Strict);
            serviceMock
                .Setup(x => x.SendNotificationAsync(It.IsAny<Notification>(), It.IsAny<string>()))
                  .Returns(Task.FromResult(new NotificationOutcome()));

            var factoryMock = new Mock<INotificationHubClientServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(ConfigConnStr, DefaultHubName))
                .Returns(serviceMock.Object);

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            //Act
            RunTest("Outputs", factoryMock.Object, testTrace);

            // Assert
            factoryMock.Verify(f => f.CreateService(ConfigConnStr, DefaultHubName), Times.Once());
            serviceMock.Verify(m => m.SendNotificationAsync(It.IsAny<Notification>(), It.IsAny<string>()), Times.Exactly(9));
            Assert.Equal("Outputs", testTrace.Events.Single().Message);
        }

        [Fact]
        public void TriggerObject()
        {
            // Arrange
            var serviceMock = new Mock<INotificationHubClientService>(MockBehavior.Strict);
            serviceMock
                .Setup(x => x.SendNotificationAsync(TestNotification, UserIdTag))
                  .Returns(Task.FromResult(new NotificationOutcome()));

            var factoryMock = new Mock<INotificationHubClientServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(ConfigConnStr, AttributeHubName))
                .Returns(serviceMock.Object);

            var jobject = JObject.FromObject(new QueueData { UserName = "TestUser", UserIdTag = UserIdTag });

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Act
            RunTest("TriggerObject", factoryMock.Object, testTrace, jobject.ToString());

            // Assert
            factoryMock.Verify(f => f.CreateService(ConfigConnStr, AttributeHubName), Times.Once());
            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal("TriggerObject", testTrace.Events[0].Message);
        }

        [Fact]
        public void ClientBinding()
        {
            // Arrange
            var factoryMock = new Mock<INotificationHubClientServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(DefaultConnStr, DefaultHubName))
                .Returns(new NotificationHubClientService(DefaultConnStr, DefaultHubName));

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Act
            RunTest("Client", factoryMock.Object, testTrace, configConnectionString: null);

            //Assert
            factoryMock.Verify(f => f.CreateService(DefaultConnStr, DefaultHubName), Times.Once());
            Assert.Equal("Client", testTrace.Events.Single().Message);
        }

        [Fact]
        public void NoConnectionString()
        {
            // Act
            var ex = Assert.Throws<FunctionInvocationException>(
                () => RunTest(typeof(NotificationHubNoConnectionStringFunctions), "Broken", new DefaultNotificationHubClientServiceFactory(), new TestTraceWriter(), configConnectionString: null, includeDefaultConnectionString: false));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        private void RunTest(string testName, INotificationHubClientServiceFactory factory, TraceWriter testTrace, object argument = null, string configConnectionString = ConfigConnStr)
        {
            RunTest(typeof(NotificationHubEndToEndFunctions), testName, factory, testTrace, argument, configConnectionString);
        }

        private void RunTest(Type testType, string testName, INotificationHubClientServiceFactory factory, TraceWriter testTrace, object argument = null, string configConnectionString = ConfigConnStr, bool includeDefaultConnectionString = true)
        {
            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);
            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = locator,
            };

            config.Tracing.Tracers.Add(testTrace);

            var arguments = new Dictionary<string, object>();
            arguments.Add("triggerData", argument);

            var notificationHubConfig = new NotificationHubsConfiguration()
            {
                ConnectionString = configConnectionString,
                NotificationHubClientServiceFactory = factory
            };

            var resolver = new TestNameResolver();
            resolver.Values.Add("HubName", "ResolvedHubName");
            resolver.Values.Add("MyConnectionString", AttributeConnStr);
            if (includeDefaultConnectionString)
            {
                resolver.Values.Add(NotificationHubsConfiguration.NotificationHubConnectionStringName, DefaultConnStr);
                resolver.Values.Add(NotificationHubsConfiguration.NotificationHubSettingName, DefaultHubName);
            }

            config.NameResolver = resolver;

            config.UseNotificationHubs(notificationHubConfig);

            JobHost host = new JobHost(config);

            host.Start();
            host.Call(testType.GetMethod(testName), arguments);
            host.Stop();
        }

        private class NotificationHubEndToEndFunctions
        {
            [NoAutomaticTrigger]
            public static void Outputs(
                            [NotificationHub] out Notification notification,
                            [NotificationHub(TagExpression = "tag")] out Notification notificationToTag,
                            [NotificationHub] out WindowsNotification windowsNotification,
                            [NotificationHub(Platform = NotificationPlatform.Wns)] out string windowsToastNotification,
                            [NotificationHub] out TemplateNotification templateNotification,
                            [NotificationHub] out string templateProperties,
                            [NotificationHub] out Notification[] notificationsArray,
                            [NotificationHub] out IDictionary<string, string> templatePropertiesDictionary,
                            [NotificationHub] IAsyncCollector<TemplateNotification> asyncCollector,
                            [NotificationHub] IAsyncCollector<string> asyncCollectorString,
                            [NotificationHub] ICollector<Notification> collector,
                            [NotificationHub] ICollector<string> collectorString,
                TraceWriter trace)
            {
                notification = GetTemplateNotification("Hi");
                notificationToTag = GetTemplateNotification("Hi tag"); string toastPayload = "<toast><visual><binding template=\"ToastText01\"><text id=\"1\">Test message</text></binding></visual></toast>";
                windowsNotification = new WindowsNotification(toastPayload);
                windowsToastNotification = @"<toast><visual><binding template=""ToastText01""><text id=""1"">Hello from a .NET App!</text></binding></visual></toast>";
                templateNotification = GetTemplateNotification("Hello");
                templateProperties = "{\"message\":\"Hello\",\"location\":\"Redmond\"}";
                templatePropertiesDictionary = GetTemplateProperties("Hello");
                notificationsArray = new TemplateNotification[]
                {
                    GetTemplateNotification("Message1"),
                    GetTemplateNotification("Message2")
                };
                trace.Warning("Outputs");
            }

            [NoAutomaticTrigger]
            public static void Client(
                [NotificationHub] NotificationHubClient client,
                TraceWriter trace)
            {
                Assert.NotNull(client);

                trace.Warning("Client");
            }

            [NoAutomaticTrigger]
            public static void TriggerObject(
              [QueueTrigger("fakequeue1")] QueueData triggerData,
              [NotificationHub(HubName = AttributeHubName, TagExpression = "{userIdTag}")] out Notification notification,
              TraceWriter trace)
            {
                notification = TestNotification;
                trace.Warning("TriggerObject");
            }

            private static TemplateNotification GetTemplateNotification(string msg)
            {
                Dictionary<string, string> templateProperties = new Dictionary<string, string>();
                templateProperties["message"] = msg;
                return new TemplateNotification(templateProperties);
            }

            private static IDictionary<string, string> GetTemplateProperties(string message)
            {
                Dictionary<string, string> templateProperties = new Dictionary<string, string>();
                templateProperties["message"] = message;
                return templateProperties;
            }
        }

        private class NotificationHubNoConnectionStringFunctions
        {
            [NoAutomaticTrigger]
            public static void Broken(
                [NotificationHub] NotificationHubClient client)
            {
            }
        }
        private class QueueData
        {
            public string UserIdTag { get; set; }
            public string UserName { get; set; }
        }
    }
}
