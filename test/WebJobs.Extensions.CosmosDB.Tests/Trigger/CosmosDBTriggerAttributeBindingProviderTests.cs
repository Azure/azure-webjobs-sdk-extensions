// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBTrigger.Tests
{
    public class CosmosDBTriggerAttributeBindingProviderTests
    {
        private readonly ILoggerFactory _loggerFactory = new LoggerFactory();
        private static readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();
        private readonly CosmosDBOptions _options = CosmosDBTestUtility.InitializeOptions("AccountEndpoint=https://fromEnvironment;AccountKey=c29tZV9rZXk=;", null).Value;

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindigsWithLeaseHostOptionsParameters
            => ValidCosmosDBTriggerBindigsWithLeaseHostOptions.GetParameters();

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindigsWithChangeFeedOptionsParameters
            => ValidCosmosDBTriggerBindigsWithChangeFeedOptions.GetParameters();

        public static IEnumerable<object[]> InvalidCosmosDBTriggerParameters
        {
            get { return InvalidCosmosDBTriggerBindings.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindingsWithAppSettingsParameters
        {
            get { return ValidCosmosDBTriggerBindingsWithAppSettings.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindingsWithDatabaseAndCollectionSettingsParameters
        {
            get { return ValidCosmosDBTriggerBindingsWithDatabaseAndCollectionSettings.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindingsDifferentConnectionsParameters
        {
            get { return ValidCosmosDBTriggerBindigsDifferentConnections.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindingsWithEnvironmentParameters
        {
            get { return ValidCosmosDBTriggerBindigsWithEnvironment.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindigsPreferredLocationsParameters
        {
            get { return ValidCosmosDBTriggerBindigsPreferredLocations.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindigsMultiMasterParameters
        {
            get { return ValidCosmosDBTriggerBindigsMultiMaster.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindingsUseDefaultJsonSerializationParameters
        {
            get { return ValidCosmosDBTriggerBindingsUseDefaultJsonSerialization.GetParameters(); }
        }

        [Theory]
        [MemberData(nameof(InvalidCosmosDBTriggerParameters))]
        public async Task InvalidParameters_Fail(ParameterInfo parameter)
        {
            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(_emptyConfig, new TestNameResolver(), _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None)));

            Assert.NotNull(ex);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsWithEnvironmentParameters))]
        public async Task ValidParametersWithEnvironment_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ConnectionStrings:CosmosDBConnectionString", "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;" }
                })
                .Build();

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyList<Document>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.DocumentCollectionLocation.Uri);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.LeaseCollectionLocation.Uri);
            Assert.Empty(binding.DocumentCollectionLocation.ConnectionPolicy.PreferredLocations);
            Assert.Empty(binding.LeaseCollectionLocation.ConnectionPolicy.PreferredLocations);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsWithAppSettingsParameters))]
        public async Task ValidParametersWithAppSettings_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values["aDatabase"] = "myDatabase";
            nameResolver.Values["aCollection"] = "myCollection";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ConnectionStrings:CosmosDBConnectionString", "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;" }
                })
                .Build();

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyList<Document>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromSettings"), binding.DocumentCollectionLocation.Uri);
            Assert.Equal(new Uri("https://fromSettings"), binding.LeaseCollectionLocation.Uri);
            Assert.Equal("myDatabase", binding.DocumentCollectionLocation.DatabaseName);
            Assert.Equal("myCollection", binding.DocumentCollectionLocation.CollectionName);
            Assert.Equal("myDatabase", binding.LeaseCollectionLocation.DatabaseName);
            Assert.Equal("leases", binding.LeaseCollectionLocation.CollectionName);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsWithDatabaseAndCollectionSettingsParameters))]
        public async Task ValidCosmosDBTriggerBindigsWithDatabaseAndCollectionSettings_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;";
            nameResolver.Values["aDatabase"] = "myDatabase";
            nameResolver.Values["aCollection"] = "myCollection";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(_emptyConfig, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyList<Document>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.DocumentCollectionLocation.Uri);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.LeaseCollectionLocation.Uri);
            Assert.Equal("myDatabase-test", binding.DocumentCollectionLocation.DatabaseName);
            Assert.Equal("myCollection-test", binding.DocumentCollectionLocation.CollectionName);
            Assert.Equal("myDatabase-test", binding.LeaseCollectionLocation.DatabaseName);
            Assert.Equal("leases", binding.LeaseCollectionLocation.CollectionName);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsDifferentConnectionsParameters))]
        public async Task ValidCosmosDBTriggerBindigsDifferentConnections_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    // Verify we load from connection strings
                    { "ConnectionStrings:CosmosDBConnectionString", "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;" },
                    { "ConnectionStrings:LeaseCosmosDBConnectionString", "AccountEndpoint=https://fromSettingsLease;AccountKey=c29tZV9rZXk=;" }
                })
                .Build();

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyList<Document>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromSettings"), binding.DocumentCollectionLocation.Uri);
            Assert.Equal(new Uri("https://fromSettingsLease"), binding.LeaseCollectionLocation.Uri);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsWithEnvironmentParameters))]
        public async Task ValidParametersWithEnvironment_ConnectionMode_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "ConnectionStrings:CosmosDBConnectionString", "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;" }
                })
                .Build();

            _options.ConnectionMode = ConnectionMode.Direct;
            _options.Protocol = Protocol.Tcp;

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyList<Document>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.DocumentCollectionLocation.Uri);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.LeaseCollectionLocation.Uri);
            Assert.Equal(ConnectionMode.Direct, binding.DocumentCollectionLocation.ConnectionPolicy.ConnectionMode);
            Assert.Equal(Protocol.Tcp, binding.DocumentCollectionLocation.ConnectionPolicy.ConnectionProtocol);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsPreferredLocationsParameters))]
        public async Task ValidCosmosDBTriggerBindigsPreferredLocationsParameters_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values["regions"] = "East US, North Europe,";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(_emptyConfig, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(2, binding.DocumentCollectionLocation.ConnectionPolicy.PreferredLocations.Count);
            Assert.Equal(2, binding.LeaseCollectionLocation.ConnectionPolicy.PreferredLocations.Count);
            Assert.Equal("East US", binding.DocumentCollectionLocation.ConnectionPolicy.PreferredLocations[0]);
            Assert.Equal("North Europe", binding.DocumentCollectionLocation.ConnectionPolicy.PreferredLocations[1]);
            Assert.Equal("East US", binding.LeaseCollectionLocation.ConnectionPolicy.PreferredLocations[0]);
            Assert.Equal("North Europe", binding.LeaseCollectionLocation.ConnectionPolicy.PreferredLocations[1]);
            Assert.False(binding.DocumentCollectionLocation.ConnectionPolicy.UseMultipleWriteLocations);
            Assert.False(binding.LeaseCollectionLocation.ConnectionPolicy.UseMultipleWriteLocations);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsMultiMasterParameters))]
        public async Task ValidCosmosDBTriggerBindigsMultiMasterParameters_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values["regions"] = "East US, North Europe,";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(_emptyConfig, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.False(binding.DocumentCollectionLocation.ConnectionPolicy.UseMultipleWriteLocations);
            Assert.True(binding.LeaseCollectionLocation.ConnectionPolicy.UseMultipleWriteLocations);

        }   

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsUseDefaultJsonSerializationParameters))]
        public async Task ValidCosmosDBTriggerBindingsUseDefaultJsonSerialization_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();

            var restoreDefaultSettings = JsonConvert.DefaultSettings;

            var defaultSettingsFetched = false;
            JsonConvert.DefaultSettings = () =>
            {
                defaultSettingsFetched = true;
                return new JsonSerializerSettings();
            };

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(_emptyConfig, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.True(defaultSettingsFetched);

            JsonConvert.DefaultSettings = restoreDefaultSettings;
        }

        [Fact]
        public void TryAndConvertToDocumentList_Fail()
        {
            Assert.False(CosmosDBTriggerBinding.TryAndConvertToDocumentList(null, out IReadOnlyList<Document> convertedValue));
            Assert.False(CosmosDBTriggerBinding.TryAndConvertToDocumentList("some weird string", out convertedValue));
            Assert.False(CosmosDBTriggerBinding.TryAndConvertToDocumentList(Guid.NewGuid(), out convertedValue));
        }

        [Fact]
        public void ResolveTimeSpanFromMilliseconds_Succeed()
        {
            TimeSpan baseTimeSpan = TimeSpan.FromMilliseconds(10);
            Assert.Equal(CosmosDBTriggerAttributeBindingProvider.ResolveTimeSpanFromMilliseconds("SomeAttribute", baseTimeSpan, null), baseTimeSpan);
            int otherMilliseconds = 20;
            TimeSpan otherTimeSpan = TimeSpan.FromMilliseconds(otherMilliseconds);
            Assert.Equal(CosmosDBTriggerAttributeBindingProvider.ResolveTimeSpanFromMilliseconds("SomeAttribute", baseTimeSpan, otherMilliseconds), otherTimeSpan);
        }

        [Fact]
        public void ResolveTimeSpanFromMilliseconds_Fail()
        {
            int otherMilliseconds = -1;
            TimeSpan baseTimeSpan = TimeSpan.FromMilliseconds(10);
            Assert.Throws<InvalidOperationException>(() => CosmosDBTriggerAttributeBindingProvider.ResolveTimeSpanFromMilliseconds("SomeAttribute", baseTimeSpan, otherMilliseconds));
        }

        [Fact]
        public void TryAndConvertToDocumentList_Succeed()
        {
            Assert.True(CosmosDBTriggerBinding.TryAndConvertToDocumentList("[{\"id\":\"123\"}]", out IReadOnlyList<Document> convertedValue));
            Assert.Equal("123", convertedValue[0].Id);

            IReadOnlyList<Document> triggerValue = new List<Document>() { new Document() { Id = "123" } };
            Assert.True(CosmosDBTriggerBinding.TryAndConvertToDocumentList(triggerValue, out convertedValue));
            Assert.Equal(triggerValue[0].Id, convertedValue[0].Id);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsWithLeaseHostOptionsParameters))]
        public async Task ValidLeaseHostOptions_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values["dynamicLeasePrefix"] = "someLeasePrefix";

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    // Verify we load from connection strings first
                    { "ConnectionStrings:CosmosDBConnectionString", "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;" },
                    { "CosmosDBConnectionString", "will not work" },
                    { "ConnectionStrings:LeaseConnectionString", "AccountEndpoint=https://overridden;AccountKey=c29tZV9rZXk=;" },
                    { "LeaseConnectionString", "will not work" }
                })
                .Build();

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            // This test uses the default for ConnectionStringSetting, but overrides LeaseConnectionStringSetting
            Assert.Equal(typeof(IReadOnlyList<Document>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.DocumentCollectionLocation.Uri);
            Assert.Equal(new Uri("https://overridden"), binding.LeaseCollectionLocation.Uri);
            Assert.Equal("someLeasePrefix", binding.ChangeFeedProcessorOptions.LeasePrefix);
            Assert.Null(binding.ChangeFeedProcessorOptions.MaxItemCount);
            Assert.False(binding.ChangeFeedProcessorOptions.StartFromBeginning);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsWithChangeFeedOptionsParameters))]
        public async Task ValidChangeFeedOptions_Succeed(ParameterInfo parameter)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    // Verify we load from root config
                    { "CosmosDBConnectionString", "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;" }
                })
                .Build();

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(config, new TestNameResolver(), _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyList<Document>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromSettings"), binding.DocumentCollectionLocation.Uri);
            Assert.Equal(new Uri("https://fromSettings"), binding.LeaseCollectionLocation.Uri);
            Assert.Equal(10, binding.ChangeFeedProcessorOptions.MaxItemCount);
            Assert.True(binding.ChangeFeedProcessorOptions.StartFromBeginning);
            Assert.NotNull(binding.ChangeFeedProcessorOptions.StartTime);
            Assert.Equal(binding.ChangeFeedProcessorOptions.StartTime, DateTime.Parse("2020-11-25T22:36:29Z"));
        }

        private static ParameterInfo GetFirstParameter(Type type, string methodName)
        {
            var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            var paramInfo = methodInfo.GetParameters().First();

            return paramInfo;
        }

        private static CosmosDBExtensionConfigProvider CreateExtensionConfigProvider(CosmosDBOptions options)
        {
            return new CosmosDBExtensionConfigProvider(new OptionsWrapper<CosmosDBOptions>(options), new DefaultCosmosDBServiceFactory(), _emptyConfig, new TestNameResolver(), NullLoggerFactory.Instance);
        }

        // These will use the default for ConnectionStringSetting, but override LeaseConnectionStringSetting
        private static class ValidCosmosDBTriggerBindigsWithLeaseHostOptions
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", LeaseConnectionStringSetting = "LeaseConnectionString", LeaseCollectionPrefix = "someLeasePrefix")] IReadOnlyList<Document> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", LeaseConnectionStringSetting = "LeaseConnectionString", LeaseCollectionPrefix = "%dynamicLeasePrefix%")] IReadOnlyList<Document> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsWithLeaseHostOptions);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") }
                };
            }
        }

        // These will set ConnectionStringSetting, which LeaseConnectionStringSetting should also use by default
        private static class ValidCosmosDBTriggerBindigsWithChangeFeedOptions
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", MaxItemsPerInvocation = 10, StartFromBeginning = true, StartFrom = "2020-11-25T22:36:29Z")] IReadOnlyList<Document> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsWithChangeFeedOptions);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") }
                };
            }
        }

        private static class InvalidCosmosDBTriggerBindings
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "notAConnectionString")] IReadOnlyList<Document> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "notAConnectionString", LeaseConnectionStringSetting = "notAConnectionString", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aCollection")] IReadOnlyList<Document> docs)
            {
            }

            public static void Func3([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", LeaseConnectionStringSetting = "CosmosDBConnectionString", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aCollection")] IReadOnlyList<Document> docs)
            {
            }

            public static void Func4([CosmosDBTrigger("aDatabase", "leases", ConnectionStringSetting = "CosmosDBConnectionString")] IReadOnlyList<Document> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(InvalidCosmosDBTriggerBindings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") },
                    new[] { GetFirstParameter(type, "Func4") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindingsWithAppSettings
        {
            public static void Func1([CosmosDBTrigger("%aDatabase%", "%aCollection%", ConnectionStringSetting = "CosmosDBConnectionString")] IReadOnlyList<Document> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("%aDatabase%", "%aCollection%", ConnectionStringSetting = "CosmosDBConnectionString", LeaseConnectionStringSetting = "CosmosDBConnectionString", LeaseDatabaseName = "%aDatabase%")] IReadOnlyList<Document> docs)
            {
            }

            public static void Func3([CosmosDBTrigger("%aDatabase%", "%aCollection%", ConnectionStringSetting = "CosmosDBConnectionString")] JArray docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindingsWithAppSettings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsDifferentConnections
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", LeaseConnectionStringSetting = "LeaseCosmosDBConnectionString")] IReadOnlyList<Document> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", LeaseConnectionStringSetting = "LeaseCosmosDBConnectionString", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aLeaseCollection")] IReadOnlyList<Document> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsDifferentConnections);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsWithEnvironment
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection")] IReadOnlyList<Document> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection")] JArray docs)
            {
            }

            public static void Func3([CosmosDBTrigger("aDatabase", "aCollection", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aLeaseCollection")] IReadOnlyList<Document> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsWithEnvironment);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindingsWithDatabaseAndCollectionSettings
        {
            public static void Func1([CosmosDBTrigger("%aDatabase%-test", "%aCollection%-test")] IReadOnlyList<Document> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("%aDatabase%-test", "%aCollection%-test")] JArray docs)
            {
            }

            public static void Func3([CosmosDBTrigger("%aDatabase%-test", "%aCollection%-test", LeaseDatabaseName = "%aDatabase%-test")] IReadOnlyList<Document> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindingsWithDatabaseAndCollectionSettings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsPreferredLocations
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", PreferredLocations = "East US, North Europe,")] IReadOnlyList<Document> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", PreferredLocations = "%regions%")] IReadOnlyList<Document> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsPreferredLocations);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsMultiMaster
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", UseMultipleWriteLocations = true)] IReadOnlyList<Document> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsMultiMaster);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindingsUseDefaultJsonSerialization
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", UseDefaultJsonSerialization = true)] IReadOnlyList<Document> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindingsUseDefaultJsonSerialization);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") }
                };
            }
        }
    }
}