// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Client;
using Microsoft.Azure.WebJobs.Extensions.Config;
using Microsoft.Azure.WebJobs.Extensions.SmtpMail;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.SmtpMail
{
    [Trait("Category", "E2E")]
    public class SmtpMailEndToEndTests
    {
        private const string DefaultConnectionString = "Default";
        private const string ConnectionString = "Config";
        private const string AttributeConnection1 = "Attribute1";
        private const string AttributeConnection2 = "Attribute2";

        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        [Fact]
        public async Task OutputBindings_WithKeysOnConfigAndAttribute()
        {
            InitializeMocks(out Mock<ISmtpMailClientFactory> factoryMock, out Mock<ISmtpMailClient> clientMock);

            var functionName = nameof(SmtpMailEndToEndFunctions.Outputs_AttributeAndConfig);
            await RunTestAsync(functionName, factoryMock.Object, connectionString: ConnectionString, includeDefaultConnection: false);

            // We expect three clients to be created. The others should be re-used because the connections match.
            factoryMock.Verify(f => f.Create("MyKey1"), Times.Once());
            factoryMock.Verify(f => f.Create("MyKey2"), Times.Once());
            factoryMock.Verify(f => f.Create(ConnectionString), Times.Once());

            // This function sends 4 messages.
            clientMock.Verify(c => c.SendMessagesAsync(It.IsAny<IList<MailMessage>>(), It.IsAny<CancellationToken>()), Times.Exactly(4));

            // Just make sure traces work.
            Assert.Equal(functionName, _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);

            factoryMock.VerifyAll();
            clientMock.VerifyAll();
        }

        [Fact]
        public async Task OutputBindings_WithNameResolver()
        {
            InitializeMocks(out Mock<ISmtpMailClientFactory> factoryMock, out Mock<ISmtpMailClient> clientMock);

            var functionName = nameof(SmtpMailEndToEndFunctions.Outputs_NameResolver);
            await RunTestAsync(functionName, factoryMock.Object, connectionString: null, includeDefaultConnection: true);

            // We expect one client to be created.
            factoryMock.Verify(f => f.Create(DefaultConnectionString), Times.Once());

            // This function sends 1 message.
            clientMock.Verify(c => c.SendMessagesAsync(It.IsAny<IList<MailMessage>>(), It.IsAny<CancellationToken>()), Times.Once);

            // Just make sure traces work.
            Assert.Equal(functionName, _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);

            factoryMock.VerifyAll();
            clientMock.VerifyAll();
        }

        [Fact]
        public async Task OutputBindings_NoConnectionString()
        {
            InitializeMocks(out Mock<ISmtpMailClientFactory> factoryMock, out Mock<ISmtpMailClient> clientMock);

            var functionName = nameof(SmtpMailEndToEndFunctions.Outputs_NameResolver);
            var exception = await Assert.ThrowsAsync<FunctionIndexingException>(() => RunTestAsync(functionName, factoryMock.Object, connectionString: null, includeDefaultConnection: false));

            Assert.Contains("SmtpMail", exception.InnerException.Message);
        }

        private void InitializeMocks(out Mock<ISmtpMailClientFactory> factoryMock, out Mock<ISmtpMailClient> clientMock)
        {
            clientMock = new Mock<ISmtpMailClient>(MockBehavior.Strict);
            clientMock.Setup(c => c.SendMessagesAsync(It.IsAny<IList<MailMessage>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            factoryMock = new Mock<ISmtpMailClientFactory>(MockBehavior.Strict);
            factoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(clientMock.Object);
        }

        private async Task RunTestAsync(string testName, ISmtpMailClientFactory factory, object argument = null, string connectionString = null, bool includeDefaultConnection = true)
        {
            Type testType = typeof(SmtpMailEndToEndFunctions);
            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);
            var resolver = new TestNameResolver();

            if (includeDefaultConnection)
            {
                resolver.Values.Add(SmtpMailConfiguration.AzureWebJobsSmtpMailKeyName, DefaultConnectionString);
            }

            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = locator,
                NameResolver = resolver,
                LoggerFactory = loggerFactory,
                DashboardConnectionString = string.Empty,
                StorageConnectionString = string.Empty,
                HostId = Guid.NewGuid().ToString("N")
            };

            var arguments = new Dictionary<string, object>
            {
                { "triggerData", argument }
            };

            var smtpMailConfig = new SmtpMailConfiguration
            {
                ConnectionString = connectionString,
                ClientFactory = factory,
                ToAddress = "ToConfig@test.com",
                FromAddress = "FromConfig@test.com"
            };

            config.UseSmtpMail(smtpMailConfig);

            using (JobHost host = new JobHost(config))
            {
                await host.StartAsync();
                await host.CallAsync(testType.GetMethod(testName), arguments);
                await host.StopAsync();
            }
        }

        private class SmtpMailEndToEndFunctions
        {
            // This function verifies Attribute and Config behavior for ConnectionString
            public static void Outputs_AttributeAndConfig(
                [SmtpMail] out MailMessage message1,
                [SmtpMail(Connection = "MyKey1")] out MailMessage message2,
                [SmtpMail(Connection = "MyKey1")] IAsyncCollector<MailMessage> asyncCollectorMessage,
                [SmtpMail(Connection = "MyKey2")] ICollector<MailMessage> collectorMessage,
                TraceWriter trace)
            {
                message1 = new MailMessage();
                message2 = new MailMessage();

                asyncCollectorMessage.AddAsync(new MailMessage()).Wait();
                collectorMessage.Add(new MailMessage());

                trace.Warning(nameof(Outputs_AttributeAndConfig));
            }

            // This function verifies Default (NameResolver) behavior for ConnectionString
            public static void Outputs_NameResolver(
                [SmtpMail] out MailMessage message,
                TraceWriter trace)
            {
                message = new MailMessage();

                trace.Warning(nameof(Outputs_NameResolver));
            }
        }
    }
}
