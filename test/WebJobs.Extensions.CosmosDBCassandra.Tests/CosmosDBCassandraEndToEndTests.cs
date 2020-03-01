// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Cassandra;
using Cassandra.Mapping;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra.Tests.Trigger
{

    public class CosmosDBCassandraEndToEndTests

    {
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();
        private ILoggerFactory _loggerFactory;
        private Mock<ITriggeredFunctionExecutor> _mockExecutor;
        private Mock<ICosmosDBCassandraService> _mockMonitoredService;
        private string _functionId;
        private TimeSpan _defaultTimeSpan;
        private string user = "cassandra-3";
        private string password = "2wVREKBUPzccE4PGrFiwmIqvKj0gUI08rGRQJkS1gPnTSDJ6s0gbWNDZZ0WkSWqAT0r6eQAdkxABO2tY8Shgig==";
        private string contactpoint = "cassandra-3.cassandra.cosmos.azure.com";
        private SSLOptions options = new SSLOptions(SslProtocols.Tls12, true, ValidateServerCertificate);
        private static readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();
        private static CosmosDBCassandraOptions _options = new CosmosDBCassandraOptions();

        public CosmosDBCassandraEndToEndTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(_loggerProvider);
            _mockExecutor = new Mock<ITriggeredFunctionExecutor>();
            _functionId = "testfunctionid";
            _mockMonitoredService = new Mock<ICosmosDBCassandraService>(MockBehavior.Strict);
            options.SetHostNameResolver((ipAddress) => contactpoint);
            _mockMonitoredService.Setup(m => m.GetCluster()).Returns(Cluster.Builder().WithCredentials(user, password).WithPort(10350).AddContactPoint(contactpoint).WithSSL(options).Build());
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindingsWithAppSettingsParameters
        {
            get { return ValidCosmosDBTriggerBindingsWithAppSettings.GetParameters(); }
        }

        private static CosmosDBCassandraExtensionConfigProvider CreateExtensionConfigProvider(CosmosDBCassandraOptions options)
        {
            return new CosmosDBCassandraExtensionConfigProvider(new OptionsWrapper<CosmosDBCassandraOptions>(options), new DefaultCosmosDBCassandraServiceFactory(), _emptyConfig, new TestNameResolver(), NullLoggerFactory.Instance);
        }

        [Fact]
        public async Task StartEndToEndTests()
        {
            var listener = new CosmosDBTriggerListener(_mockExecutor.Object, _functionId, "uprofile", "user", true, _defaultTimeSpan.Milliseconds, _mockMonitoredService.Object, _loggerFactory.CreateLogger<CosmosDBTriggerListener>());

            //write the records
            options.SetHostNameResolver((ipAddress) => contactpoint);
            Cluster cluster = Cluster.Builder().WithCredentials(user, password).WithPort(10350).AddContactPoint(contactpoint).WithSSL(options).Build();
            ISession session = cluster.Connect();
            session = cluster.Connect("uprofile");
            session.Execute("CREATE TABLE IF NOT EXISTS uprofile.user (user_id int PRIMARY KEY, user_name text, user_bcity text)");
            Thread.Sleep(2000);
            IMapper mapper = new Mapper(session);


            //start the listener
            _ = listener.StartAsync(CancellationToken.None);

            await TestHelpers.Await(() =>
            {
                //insert row and return true when change picked up in log
                mapper.Insert<User>(new User(1, "field1", "field2"));
                System.Diagnostics.Debug.WriteLine("log: " + _loggerProvider.GetLogString());
                return _loggerProvider.GetLogString().Contains("processing change...");
            }).ConfigureAwait(true);

        }


        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsWithAppSettingsParameters))]
        public async Task ValidParametersWithAppSettings_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ConnectionStrings:CassandraDBconnection", "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;" },
                    { "ContactPoint", contactpoint },
                    { "User", user },
                    { "Password", password }
                })
                .Build();

            CosmosDBCassandraTriggerAttributeBindingProvider provider = new CosmosDBCassandraTriggerAttributeBindingProvider(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);
            CosmosDBCassandraTriggerBinding binding = (CosmosDBCassandraTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None)).ConfigureAwait(true);
            Assert.Equal(typeof(IReadOnlyList<JArray>), binding.TriggerValueType);
        }



        private static ParameterInfo GetFirstParameter(Type type, string methodName)
        {
            var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            var paramInfo = methodInfo.GetParameters().First();

            return paramInfo;
        }

        private static class ValidCosmosDBTriggerBindingsWithAppSettings
        {
            public static void Func1(
                [CosmosDBCassandraTrigger("uprofile", "user",
                ContactPoint = "ContactPoint",
                FeedPollDelay = 5000,
                User = "User",
                StartFromBeginning = true,
                Password = "Password")]IReadOnlyList<JArray> input,
                ILogger log)
            {
            }
            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindingsWithAppSettings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") }
                };
            }
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // Do not allow this client to communicate with unauthenticated servers.
            throw new InvalidOperationException($"Certificate error: {sslPolicyErrors}");

        }
        private class User
        {
            public int user_id { get; set; }

            public String user_name { get; set; }

            public String user_bcity { get; set; }

            public User(int user_id, String user_name, String user_bcity)
            {
                this.user_id = user_id;
                this.user_name = user_name;
                this.user_bcity = user_bcity;
            }

            public override String ToString()
            {
                return String.Format(" {0} | {1} | {2} ", user_id, user_name, user_bcity);
            }
        }
    }
}