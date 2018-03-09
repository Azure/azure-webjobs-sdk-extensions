// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Client;
using Microsoft.Azure.WebJobs.Extensions.Config;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Moq;
using Newtonsoft.Json.Linq;
using SendGrid.Helpers.Mail;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.SendGrid
{
    [Trait("Category", "E2E")]
    public class SendGridEndToEndTests
    {
        private const string DefaultApiKey = "Default";
        private const string ConfigApiKey = "Config";
        private const string AttributeApiKey1 = "Attribute1";
        private const string AttributeApiKey2 = "Attribute2";

        [Fact]
        public async Task OutputBindings_WithKeysOnConfigAndAttribute()
        {
            string functionName = nameof(SendGridEndToEndFunctions.Outputs_AttributeAndConfig);

            Mock<ISendGridClientFactory> factoryMock;
            Mock<ISendGridClient> clientMock;
            InitializeMocks(out factoryMock, out clientMock);

            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Warning);

            await RunTestAsync(functionName, factoryMock.Object, testTrace, configApiKey: ConfigApiKey, includeDefaultApiKey: false);

            // We expect three clients to be created. The others should be re-used because the ApiKeys match.
            factoryMock.Verify(f => f.Create(AttributeApiKey1), Times.Once());
            factoryMock.Verify(f => f.Create(AttributeApiKey2), Times.Once());
            factoryMock.Verify(f => f.Create(ConfigApiKey), Times.Once());

            // This function sends 4 messages.
            clientMock.Verify(c => c.SendMessageAsync(It.IsAny<string>()), Times.Exactly(4));

            // Just make sure traces work.
            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal(functionName, testTrace.Events[0].Message);

            factoryMock.VerifyAll();
            clientMock.VerifyAll();
        }

        [Fact]
        public async Task OutputBindings_WithNameResolver()
        {
            string functionName = nameof(SendGridEndToEndFunctions.Outputs_NameResolver);

            Mock<ISendGridClientFactory> factoryMock;
            Mock<ISendGridClient> clientMock;
            InitializeMocks(out factoryMock, out clientMock);

            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Warning);

            await RunTestAsync(functionName, factoryMock.Object, testTrace, configApiKey: null, includeDefaultApiKey: true);

            // We expect one client to be created.
            factoryMock.Verify(f => f.Create(DefaultApiKey), Times.Once());

            // This function sends 1 message.
            clientMock.Verify(c => c.SendMessageAsync(It.IsAny<string>()), Times.Once);

            // Just make sure traces work.
            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal(functionName, testTrace.Events[0].Message);

            factoryMock.VerifyAll();
            clientMock.VerifyAll();
        }

        [Fact]
        public async Task OutputBindings_NoApiKey()
        {
            string functionName = nameof(SendGridEndToEndFunctions.Outputs_NameResolver);

            Mock<ISendGridClientFactory> factoryMock;
            Mock<ISendGridClient> clientMock;
            InitializeMocks(out factoryMock, out clientMock);

            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Warning);

            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(functionName, factoryMock.Object, testTrace, configApiKey: null, includeDefaultApiKey: false));

            Assert.Equal("The SendGrid ApiKey must be set either via an 'AzureWebJobsSendGridApiKey' app setting, via an 'AzureWebJobsSendGridApiKey' environment variable, or directly in code via SendGridConfiguration.ApiKey or SendGridAttribute.ApiKey.", ex.InnerException.Message);
        }

        private void InitializeMocks(out Mock<ISendGridClientFactory> factoryMock, out Mock<ISendGridClient> clientMock)
        {
            clientMock = new Mock<ISendGridClient>(MockBehavior.Strict);
            clientMock
                .Setup(c => c.SendMessageAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(0));

            factoryMock = new Mock<ISendGridClientFactory>(MockBehavior.Strict);
            factoryMock
                    .Setup(f => f.Create(It.IsAny<string>()))
                    .Returns(clientMock.Object);
        }

        private async Task RunTestAsync(string testName, ISendGridClientFactory factory, TestTraceWriter testTrace, object argument = null,
            string configApiKey = null, bool includeDefaultApiKey = true)
        {
            Type testType = typeof(SendGridEndToEndFunctions);
            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);
            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = locator,
            };

            config.Tracing.Tracers.Add(testTrace);

            var arguments = new Dictionary<string, object>();
            arguments.Add("triggerData", argument);

            var sendGridConfig = new SendGridConfiguration
            {
                ApiKey = configApiKey,
                ClientFactory = factory,
                ToAddress = new Email("ToConfig@test.com"),
                FromAddress = new Email("FromConfig@test.com")
            };

            var resolver = new TestNameResolver();
            resolver.Values.Add("MyKey1", AttributeApiKey1);
            resolver.Values.Add("MyKey2", AttributeApiKey2);

            if (includeDefaultApiKey)
            {
                resolver.Values.Add(SendGridConfiguration.AzureWebJobsSendGridApiKeyName, DefaultApiKey);
            }

            config.NameResolver = resolver;

            config.UseSendGrid(sendGridConfig);

            JobHost host = new JobHost(config);

            await host.StartAsync();
            testTrace.Events.Clear();
            await host.CallAsync(testType.GetMethod(testName), arguments);
            await host.StopAsync();
        }

        private class SendGridEndToEndFunctions
        {
            /// This function verifies Attribute and Config behavior for ApiKey
            public static void Outputs_AttributeAndConfig(
                [SendGrid(ApiKey = "MyKey1")] out Mail message,
                [SendGrid] out JObject jObject,
                [SendGrid(ApiKey = "MyKey1")] IAsyncCollector<Mail> asyncCollectorMessage,
                [SendGrid(ApiKey = "MyKey2")] ICollector<JObject> collectorJObject,
                TraceWriter trace)
            {
                message = new Mail();

                jObject = JObject.Parse(@"{
                  'personalizations': [
                    {
                      'to': [
                        {
                          'email': 'ToFunction@test.com'
                        }
                      ]
                    }
                  ],
                  'from': {
                    'email': 'FromFunction@test.com'
                  }
                }");

                asyncCollectorMessage.AddAsync(new Mail()).Wait();
                collectorJObject.Add(new JObject());

                trace.Warning(nameof(Outputs_AttributeAndConfig));
            }

            /// This function verifies Default (NameResolver) behavior for ApiKey
            public static void Outputs_NameResolver(
                [SendGrid] out Mail message,
                TraceWriter trace)
            {
                message = new Mail();
                trace.Warning(nameof(Outputs_NameResolver));
            }
        }
    }
}
