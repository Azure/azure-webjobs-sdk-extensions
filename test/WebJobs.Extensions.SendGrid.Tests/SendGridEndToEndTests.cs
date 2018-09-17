// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Config;
using Microsoft.Azure.WebJobs.Extensions.SendGrid;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using SendGrid.Helpers.Mail;
using Xunit;

namespace SendGridTests
{
    [Trait("Category", "E2E")]
    public class SendGridEndToEndTests
    {
        private const string DefaultApiKey = "Default";
        private const string ConfigApiKey = "Config";
        private const string AttributeApiKey1 = "Attribute1";
        private const string AttributeApiKey2 = "Attribute2";

        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        [Fact]
        public async Task OutputBindings_WithKeysOnConfigAndAttribute()
        {
            string functionName = nameof(SendGridEndToEndFunctions.Outputs_AttributeAndConfig);
            InitializeMocks(out Mock<ISendGridClientFactory> factoryMock, out Mock<ISendGridClient> clientMock);

            await RunTestAsync(functionName, factoryMock.Object, configApiKey: ConfigApiKey, includeDefaultApiKey: false);

            // We expect three clients to be created. The others should be re-used because the ApiKeys match.
            factoryMock.Verify(f => f.Create(AttributeApiKey1), Times.Once());
            factoryMock.Verify(f => f.Create(AttributeApiKey2), Times.Once());
            factoryMock.Verify(f => f.Create(ConfigApiKey), Times.Once());

            // This function sends 4 messages.
            clientMock.Verify(c => c.SendMessageAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(4));

            // Just make sure traces work.
            Assert.Equal(functionName, _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);

            factoryMock.VerifyAll();
            clientMock.VerifyAll();
        }

        [Fact]
        public async Task OutputBindings_WithNameResolver()
        {
            string functionName = nameof(SendGridEndToEndFunctions.Outputs_NameResolver);
            InitializeMocks(out Mock<ISendGridClientFactory> factoryMock, out Mock<ISendGridClient> clientMock);

            await RunTestAsync(functionName, factoryMock.Object, configApiKey: null, includeDefaultApiKey: true);

            // We expect one client to be created.
            factoryMock.Verify(f => f.Create(DefaultApiKey), Times.Once());

            // This function sends 1 message.
            clientMock.Verify(c => c.SendMessageAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()), Times.Once);

            // Just make sure traces work.
            Assert.Equal(functionName, _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);

            factoryMock.VerifyAll();
            clientMock.VerifyAll();
        }

        [Fact]
        public async Task OutputBindings_NoApiKey()
        {
            string functionName = nameof(SendGridEndToEndFunctions.Outputs_NameResolver);
            InitializeMocks(out Mock<ISendGridClientFactory> factoryMock, out Mock<ISendGridClient> clientMock);

            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(functionName, factoryMock.Object, configApiKey: null, includeDefaultApiKey: false));

            Assert.Equal("The SendGrid ApiKey must be set either via an 'AzureWebJobsSendGridApiKey' app setting, via an 'AzureWebJobsSendGridApiKey' environment variable, or directly in code via SendGridOptions.ApiKey or SendGridAttribute.ApiKey.", ex.InnerException.Message);
        }

        private void InitializeMocks(out Mock<ISendGridClientFactory> factoryMock, out Mock<ISendGridClient> clientMock)
        {
            var mockResponse = new SendGrid.Response(HttpStatusCode.OK, null, null);
            clientMock = new Mock<Client.ISendGridClient>(MockBehavior.Strict);
            clientMock
                .Setup(c => c.SendMessageAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse);

            factoryMock = new Mock<ISendGridClientFactory>(MockBehavior.Strict);
            factoryMock
                    .Setup(f => f.Create(It.IsAny<string>()))
                    .Returns(clientMock.Object);
        }

        private async Task RunTestAsync(string testName, ISendGridClientFactory factory, object argument = null, string configApiKey = null, bool includeDefaultApiKey = true)
        {
            Type testType = typeof(SendGridEndToEndFunctions);
            var locator = new ExplicitTypeLocator(testType);
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);

            var arguments = new Dictionary<string, object>();
            arguments.Add("triggerData", argument);

            var resolver = new TestNameResolver();

            IHost host = new HostBuilder()
                .ConfigureWebJobs(builder =>
                {
                    builder.AddSendGrid(o =>
                    {
                        if (configApiKey != null)
                        {
                            o.ApiKey = configApiKey;
                        }

                        o.ToAddress = new EmailAddress("ToConfig@test.com");
                        o.FromAddress = new EmailAddress("FromConfig@test.com");
                    });
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ISendGridClientFactory>(factory);
                    services.AddSingleton<INameResolver>(resolver);
                    services.AddSingleton<ITypeLocator>(locator);
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(_loggerProvider);
                })
                .ConfigureAppConfiguration(c =>
                {
                    c.Sources.Clear();
                    var collection = new Dictionary<string, string>
                    {
                        { "MyKey1", AttributeApiKey1 },
                        { "MyKey2", AttributeApiKey2 }
                    };

                    if (includeDefaultApiKey)
                    {
                        collection.Add(SendGridExtensionConfigProvider.AzureWebJobsSendGridApiKeyName, DefaultApiKey);
                    }

                    c.AddInMemoryCollection(collection);
                })
                .Build();

            await host.GetJobHost().CallAsync(testType.GetMethod(testName), arguments);
        }

        private class SendGridEndToEndFunctions
        {
            // This function verifies Attribute and Config behavior for ApiKey
            public static void Outputs_AttributeAndConfig(
                [SendGrid(ApiKey = "MyKey1")] out SendGridMessage message,
                [SendGrid] out JObject jObject,
                [SendGrid(ApiKey = "MyKey1")] IAsyncCollector<SendGridMessage> asyncCollectorMessage,
                [SendGrid(ApiKey = "MyKey2")] ICollector<JObject> collectorJObject,
                TraceWriter trace)
            {
                message = new SendGridMessage();

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

                asyncCollectorMessage.AddAsync(new SendGridMessage()).Wait();
                collectorJObject.Add(new JObject());

                trace.Warning(nameof(Outputs_AttributeAndConfig));
            }

            // This function verifies Default (NameResolver) behavior for ApiKey
            public static void Outputs_NameResolver(
                [SendGrid] out SendGridMessage message,
                TraceWriter trace)
            {
                message = new SendGridMessage();
                trace.Warning(nameof(Outputs_NameResolver));
            }
        }
    }
}
