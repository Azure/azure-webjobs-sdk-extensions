// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra.Tests.Trigger
{
    public class CosmosDBTriggerAttributeBindingProviderTests
    {

        private readonly ILoggerFactory _loggerFactory = new LoggerFactory();
        private static readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();
        private static CosmosDBCassandraOptions _options = new CosmosDBCassandraOptions();

        public static IEnumerable<object[]> InvalidCosmosDBTriggerParameters
        {
            get { return InvalidCosmosDBTriggerBindings.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindingsWithAppSettingsParameters
        {
            get { return ValidCosmosDBTriggerBindingsWithAppSettings.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindingsDifferentConnectionsParameters
        {
            get { return ValidCosmosDBTriggerBindigsDifferentConnections.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindingsWithEnvironmentParameters
        {
            get { return ValidCosmosDBTriggerBindigsWithEnvironment.GetParameters(); }
        }

        [Theory]
        [MemberData(nameof(InvalidCosmosDBTriggerParameters))]
        public async Task InvalidParameters_Fail(ParameterInfo parameter)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "MyContactPoint", "tvkcassandra.cassandra.cosmos.azure.com" },
                    { "MyUser", "tvkcassandra" },
                    { "MyPassword", "fOUhRd1tue9DV7oshoDsKiXLamfMHemZ2EjJd9Q8JEjkJEfdPDqyv8HLlPOuxpbIp8XjbAHfrYpJJLubDvCWIQ==" }
                })
                .Build();

            CosmosDBCassandraTriggerAttributeBindingProvider provider = new CosmosDBCassandraTriggerAttributeBindingProvider(config, new TestNameResolver(), _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None)));

            Assert.NotNull(ex);
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
                    { "ContactPoint", "tvkcassandra.cassandra.cosmos.azure.com" },
                    { "User", "tvkcassandra" },
                    { "Password", "fOUhRd1tue9DV7oshoDsKiXLamfMHemZ2EjJd9Q8JEjkJEfdPDqyv8HLlPOuxpbIp8XjbAHfrYpJJLubDvCWIQ==" }
                })
                .Build();

            CosmosDBCassandraTriggerAttributeBindingProvider provider = new CosmosDBCassandraTriggerAttributeBindingProvider(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBCassandraTriggerBinding binding = (CosmosDBCassandraTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyList<JArray>), binding.TriggerValueType);
        }

        [Fact]
        public void TryAndConvertToDocumentList_Fail()
        {
            Assert.False(CosmosDBCassandraTriggerBinding.TryAndConvertToDocumentList(null, out IReadOnlyList<JArray> convertedValue));
            Assert.False(CosmosDBCassandraTriggerBinding.TryAndConvertToDocumentList("some weird string", out convertedValue));
            Assert.False(CosmosDBCassandraTriggerBinding.TryAndConvertToDocumentList(Guid.NewGuid(), out convertedValue));
        }

        private static ParameterInfo GetFirstParameter(Type type, string methodName)
        {
            var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            var paramInfo = methodInfo.GetParameters().First();

            return paramInfo;
        }

        private static CosmosDBCassandraExtensionConfigProvider CreateExtensionConfigProvider(CosmosDBCassandraOptions options)
        {
            return new CosmosDBCassandraExtensionConfigProvider(new OptionsWrapper<CosmosDBCassandraOptions>(options), new DefaultCosmosDBCassandraServiceFactory(), _emptyConfig, new TestNameResolver(), NullLoggerFactory.Instance);
        }


        private static class InvalidCosmosDBTriggerBindings
        {
            public static void Func1([CosmosDBCassandraTrigger(
                "data",
                "table1",
                ContactPoint = "MyContactPoint",
                FeedPollDelay = 5000,
                User = "MyUser",
                Password = "Not Specified")] IReadOnlyList<JArray> input)
            {
            }

            public static void Func2([CosmosDBCassandraTrigger(
                "data",
                "table1",
                ContactPoint = "MyContactPoint",
                FeedPollDelay = 5000,
                User = "Not Specified",
                Password = "MyPassword")] IReadOnlyList<JArray> input)
            {
            }

            public static void Func3([CosmosDBCassandraTrigger(
                "data",
                "table1",
                ContactPoint = "Not Specified",
                FeedPollDelay = 5000,
                User = "MyUser",
                Password = "MyPassword")] IReadOnlyList<JArray> input)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(InvalidCosmosDBTriggerBindings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindingsWithAppSettings
        {
            public static void Func1([CosmosDBCassandraTrigger(
                "data",
                "table1",
                ContactPoint = "ContactPoint",
                FeedPollDelay = 5000,
                User = "User",
                Password = "Password")] IReadOnlyList<JArray> input)
            {
            }

            // TODO more tests?
            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindingsWithAppSettings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsDifferentConnections
        {

            public static void Func1([CosmosDBCassandraTrigger(
                "data",
                "table1",
                ContactPoint = "ContactPoint",
                FeedPollDelay = 5000,
                User = "User",
                Password = "Password")] IReadOnlyList<JArray> input)
            {
            }


            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsDifferentConnections);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsWithEnvironment
        {

            public static void Func1([CosmosDBCassandraTrigger(
                "data",
                "table1",
                ContactPoint = "ContactPoint",
                FeedPollDelay = 5000,
                User = "User",
                Password = "Password")] IReadOnlyList<JArray> input)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsWithEnvironment);

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

    }
}