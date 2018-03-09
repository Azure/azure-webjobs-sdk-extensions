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
    [Trait("Category", "E2E")]
    public class NotificationHubEndToEndTests
    {
        private const string AttributeConnStr = "Endpoint=sb://AttrTestNS.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=RandomKey";
        private const string AttributeHubName = "AttributeHubName";
        private const string ConfigConnStr = "Endpoint=sb://ConfigTestNS.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=RandomKey";
        private const string ConfigHubName = "ConfigTestHub";
        private const string DefaultConnStr = "Endpoint=sb://DefaultTestNS.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=RandomKey";
        private const string DefaultHubName = "defaulttesthub";
        private const string MessagePropertiesJSON = "{\"message\":\"Hello\",\"location\":\"Redmond\"}";
        private const string WindowsToastPayload = "<toast><visual><binding template=\"ToastText01\"><text id=\"1\">Test message</text></binding></visual></toast>";
        private const string UserIdTag = "myuserid123";
        private static Notification testNotification = Converter.BuildTemplateNotificationFromJsonString(MessagePropertiesJSON);

        [Fact]
        public void OutputBindings()
        {
            SetupAndVerifyOutputBindings("Outputs", 11);
        }

        [Fact]
        public void OutputBindingsAsyncCollector()
        {
            SetupAndVerifyOutputBindings("OutputsAsyncCollector", 2);
        }

        [Fact]
        public void TriggerObject()
        {
            // Arrange
            var serviceMock = new Mock<INotificationHubClientService>(MockBehavior.Strict);
            serviceMock
                .Setup(x => x.SendNotificationAsync(testNotification, UserIdTag))
                  .Returns(Task.FromResult(new NotificationOutcome()));

            var factoryMock = new Mock<INotificationHubClientServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(ConfigConnStr, AttributeHubName.ToLowerInvariant(), It.IsAny<bool>()))
                .Returns(serviceMock.Object);

            var jobject = JObject.FromObject(new QueueData { UserName = "TestUser", UserIdTag = UserIdTag });

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Act
            RunTest("TriggerObject", factoryMock.Object, testTrace, jobject.ToString());

            // Assert
            factoryMock.Verify(f => f.CreateService(ConfigConnStr, AttributeHubName.ToLowerInvariant(), false), Times.Once());
            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal("TriggerObject", testTrace.Events[0].Message);
        }

        [Fact]
        public void ClientBinding()
        {
            var factoryMock = new Mock<INotificationHubClientServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(DefaultConnStr, DefaultHubName, It.IsAny<bool>()))
                .Returns(new NotificationHubClientService(DefaultConnStr, DefaultHubName));

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Act
            RunTest("Client", factoryMock.Object, testTrace, configConnectionString: null, configHubName: null);

            //Assert
            factoryMock.Verify(f => f.CreateService(DefaultConnStr, DefaultHubName, false), Times.Once());
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

        [Fact]
        public void NoHubName()
        {
            // Act
            var ex = Assert.Throws<FunctionInvocationException>(
                () => RunTest(typeof(NotificationHubNoConnectionStringFunctions), "Broken", new DefaultNotificationHubClientServiceFactory(), new TestTraceWriter(), configHubName: null, includeDefaultHubName: false));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        private void SetupAndVerifyOutputBindings(string test, int invokeSendNotificationTimes)
        {
            var serviceMock = new Mock<INotificationHubClientService>(MockBehavior.Strict);
            serviceMock
                .Setup(x => x.SendNotificationAsync(It.IsAny<Notification>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new NotificationOutcome()));

            var factoryMock = new Mock<INotificationHubClientServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(ConfigConnStr, ConfigHubName.ToLowerInvariant(), It.IsAny<bool>()))
                .Returns(serviceMock.Object);

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            //Act
            RunTest(test, factoryMock.Object, testTrace);

            // Assert
            factoryMock.Verify(f => f.CreateService(ConfigConnStr, ConfigHubName.ToLowerInvariant(), false), Times.Once());
            serviceMock.Verify(m => m.SendNotificationAsync(It.IsAny<Notification>(), It.IsAny<string>()), Times.Exactly(invokeSendNotificationTimes));
            Assert.Equal(test, testTrace.Events.Single().Message);
        }

        private void RunTest(string testName, INotificationHubClientServiceFactory factory, TestTraceWriter testTrace, object argument = null, string configConnectionString = ConfigConnStr, string configHubName = ConfigHubName)
        {
            RunTest(typeof(NotificationHubEndToEndFunctions), testName, factory, testTrace, argument, configConnectionString, configHubName);
        }

        private void RunTest(Type testType, string testName, INotificationHubClientServiceFactory factory, TestTraceWriter testTrace, object argument = null, string configConnectionString = ConfigConnStr, string configHubName = ConfigHubName, bool includeDefaultConnectionString = true, bool includeDefaultHubName = true)
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
                HubName = configHubName,
                NotificationHubClientServiceFactory = factory
            };

            var resolver = new TestNameResolver();
            resolver.Values.Add("HubName", "ResolvedHubName");
            resolver.Values.Add("MyConnectionString", AttributeConnStr);
            if (includeDefaultConnectionString)
            {
                resolver.Values.Add(NotificationHubsConfiguration.NotificationHubConnectionStringName, DefaultConnStr);
            }
            if (includeDefaultHubName)
            {
                resolver.Values.Add(NotificationHubsConfiguration.NotificationHubSettingName, DefaultHubName);
            }

            config.NameResolver = resolver;

            config.UseNotificationHubs(notificationHubConfig);

            JobHost host = new JobHost(config);
            host.Start();
            testTrace.Events.Clear();
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
                            [NotificationHub(Platform = NotificationPlatform.Wns)] out string windowsToastNotificationAsString,
                            [NotificationHub] out TemplateNotification templateNotification,
                            [NotificationHub] out string templateProperties,
                            [NotificationHub] out Notification[] notificationsArray,
                            [NotificationHub] out IDictionary<string, string> templatePropertiesDictionary,
                            [NotificationHub] ICollector<Notification> collector,
                            [NotificationHub] ICollector<string> collectorString,
                TraceWriter trace)
            {
                notification = GetTemplateNotification("Hi");
                notificationToTag = GetTemplateNotification("Hi tag");
                windowsNotification = new WindowsNotification(WindowsToastPayload);
                windowsToastNotificationAsString = WindowsToastPayload;
                templateNotification = GetTemplateNotification("Hello");
                templateProperties = MessagePropertiesJSON;
                templatePropertiesDictionary = GetTemplateProperties("Hello");
                notificationsArray = new TemplateNotification[]
                {
                    GetTemplateNotification("Message1"),
                    GetTemplateNotification("Message2")
                };
                collector.Add(GetTemplateNotification("Hi"));
                collectorString.Add(MessagePropertiesJSON);
                trace.Warning("Outputs");
            }

            [NoAutomaticTrigger]
            public static async Task OutputsAsyncCollector(
                           [NotificationHub] IAsyncCollector<TemplateNotification> asyncCollector,
                           [NotificationHub] IAsyncCollector<string> asyncCollectorString,
               TraceWriter trace)
            {
                await asyncCollector.AddAsync(GetTemplateNotification("Hello"));
                await asyncCollectorString.AddAsync(MessagePropertiesJSON);
                trace.Warning("OutputsAsyncCollector");
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
                notification = testNotification;
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
