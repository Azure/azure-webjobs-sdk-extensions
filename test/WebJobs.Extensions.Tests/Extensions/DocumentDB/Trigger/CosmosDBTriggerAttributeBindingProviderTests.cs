// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Trigger
{
    public class CosmosDBTriggerAttributeBindingProviderTests
    {
        [Theory]
        [MemberData(nameof(InvalidCosmosDBTriggerParameters))]
        public async Task InvalidParameters_Fail(ParameterInfo parameter)
        {
            var testTrace = new TestTraceWriter(TraceLevel.Verbose);

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(new DefaultNameResolver(), testTrace, CreateConfiguration());

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None)));

            Assert.NotNull(ex);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsWithEnvironmentParameters))]
        public async Task ValidParametersWithEnvironment_Succeed(ParameterInfo parameter)
        {
            var testTrace = new TestTraceWriter(TraceLevel.Verbose);
            var nameResolver = new TestNameResolver();
            nameResolver.Values[DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, testTrace, CreateConfiguration());

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
            Assert.Equal(binding.DocumentCollectionLocation.Uri, new Uri("https://fromEnvironment"));
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsWithEnvironmentParameters))]
        public async Task ValidParametersWithEnvironment_ConnectionMode_Succeed(ParameterInfo parameter)
        {
            var testTrace = new TestTraceWriter(TraceLevel.Verbose);
            var nameResolver = new TestNameResolver();
            nameResolver.Values[DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, testTrace, CreateConfigurationWithConnectionMode());

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
            Assert.Equal(binding.DocumentCollectionLocation.Uri, new Uri("https://fromEnvironment"));
            Assert.Equal(binding.DocumentCollectionLocation.ConnectionPolicy.ConnectionMode, ConnectionMode.Direct);
            Assert.Equal(binding.DocumentCollectionLocation.ConnectionPolicy.ConnectionProtocol, Protocol.Tcp);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsWithAppSettingsParameters))]
        public async Task ValidParametersWithAppSettings_Succeed(ParameterInfo parameter)
        {
            var testTrace = new TestTraceWriter(TraceLevel.Verbose);
            var nameResolver = new TestNameResolver();
            nameResolver.Values[DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";
            nameResolver.Values["aDatabase"] = "myDatabase";
            nameResolver.Values["aCollection"] = "myCollection";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, testTrace, CreateConfiguration());

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
            Assert.Equal(binding.DocumentCollectionLocation.Uri, new Uri("https://fromSettings"));
            Assert.Equal(binding.DocumentCollectionLocation.DatabaseName, "myDatabase");
            Assert.Equal(binding.DocumentCollectionLocation.CollectionName, "myCollection");
            Assert.Equal(binding.LeaseCollectionLocation.DatabaseName, "myDatabase");
            Assert.Equal(binding.LeaseCollectionLocation.CollectionName, "leases");
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsWithDatabaseAndCollectionSettingsParameters))]
        public async Task ValidCosmosDBTriggerBindigsWithDatabaseAndCollectionSettings_Succeed(ParameterInfo parameter)
        {
            var testTrace = new TestTraceWriter(TraceLevel.Verbose);
            var nameResolver = new TestNameResolver();
            nameResolver.Values[DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";
            nameResolver.Values["aDatabase"] = "myDatabase";
            nameResolver.Values["aCollection"] = "myCollection";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, testTrace, CreateConfiguration());

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
            Assert.Equal(binding.DocumentCollectionLocation.Uri, new Uri("https://fromEnvironment"));
            Assert.Equal(binding.DocumentCollectionLocation.DatabaseName, "myDatabase-test");
            Assert.Equal(binding.DocumentCollectionLocation.CollectionName, "myCollection-test");
            Assert.Equal(binding.LeaseCollectionLocation.DatabaseName, "myDatabase-test");
            Assert.Equal(binding.LeaseCollectionLocation.CollectionName, "leases");
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsDifferentConnectionsParameters))]
        public async Task ValidCosmosDBTriggerBindigsDifferentConnections_Succeed(ParameterInfo parameter)
        {
            var testTrace = new TestTraceWriter(TraceLevel.Verbose);
            var nameResolver = new TestNameResolver();
            nameResolver.Values[DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";
            nameResolver.Values["LeaseCosmosDBConnectionString"] = "AccountEndpoint=https://fromSettingsLease;AccountKey=someKey;";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, testTrace, CreateConfiguration());

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
            Assert.Equal(binding.DocumentCollectionLocation.Uri, new Uri("https://fromSettings"));
            Assert.Equal(binding.LeaseCollectionLocation.Uri, new Uri("https://fromSettingsLease"));
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsWithLeaseHostOptionsParameters))]
        public async Task ValidLeaseHostOptions_Succeed(ParameterInfo parameter)
        {
            var testTrace = new TestTraceWriter(TraceLevel.Verbose);
            var nameResolver = new TestNameResolver();
            nameResolver.Values[DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";
            nameResolver.Values["dynamicLeasePrefix"] = "someLeasePrefix";

            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, testTrace, CreateConfiguration());

            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
            Assert.Equal(binding.DocumentCollectionLocation.Uri, new Uri("https://fromSettings"));
            Assert.Equal(binding.ChangeFeedHostOptions.LeasePrefix, "someLeasePrefix");
            Assert.Null(binding.ChangeFeedOptions.MaxItemCount);
            Assert.False(binding.ChangeFeedOptions.StartFromBeginning);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsWithChangeFeedOptionsParameters))]
        public async Task ValidChangeFeedOptions_Succeed(ParameterInfo parameter)
        {
            var testTrace = new TestTraceWriter(TraceLevel.Verbose);
            var nameResolver = new TestNameResolver();
            nameResolver.Values[DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName] = "AccountEndpoint=https://fromEnvironment;AccountKey=someKey;";
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=someKey;";
            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider(nameResolver, testTrace, CreateConfiguration());
            CosmosDBTriggerBinding binding = (CosmosDBTriggerBinding)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));
            Assert.Equal(typeof(IReadOnlyList<Document>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromSettings"), binding.DocumentCollectionLocation.Uri);
            Assert.Equal(10, binding.ChangeFeedOptions.MaxItemCount);
            Assert.True(binding.ChangeFeedOptions.StartFromBeginning);
        }

    [Fact]
        public void TryAndConvertToDocumentList_Fail()
        {
            IReadOnlyList<Document> convertedValue;
            Assert.False(CosmosDBTriggerBinding.TryAndConvertToDocumentList(null, out convertedValue));
            Assert.False(CosmosDBTriggerBinding.TryAndConvertToDocumentList("some weird string", out convertedValue));
            Assert.False(CosmosDBTriggerBinding.TryAndConvertToDocumentList(Guid.NewGuid(), out convertedValue));
        }

        [Fact]
        public void TryAndConvertToDocumentList_Succeed()
        {
            IReadOnlyList<Document> convertedValue;
            Assert.True(CosmosDBTriggerBinding.TryAndConvertToDocumentList("[{\"id\":\"123\"}]", out convertedValue));
            Assert.Equal("123", convertedValue[0].Id);

            IReadOnlyList<Document> triggerValue = new List<Document>() { new Document() { Id = "123" } };
            Assert.True(CosmosDBTriggerBinding.TryAndConvertToDocumentList(triggerValue, out convertedValue));
            Assert.Equal(triggerValue[0].Id, convertedValue[0].Id);
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

        public static IEnumerable<ParameterInfo[]> InvalidCosmosDBTriggerParameters
        {
            get { return InvalidCosmosDBTriggerBindigs.GetParameters(); }
        }

        public static IEnumerable<ParameterInfo[]> ValidCosmosDBTriggerBindingsWithAppSettingsParameters
        {
            get { return ValidCosmosDBTriggerBindigsWithAppSettings.GetParameters(); }
        }

        public static IEnumerable<ParameterInfo[]> ValidCosmosDBTriggerBindingsWithDatabaseAndCollectionSettingsParameters
        {
            get { return ValidCosmosDBTriggerBindingsWithDatabaseAndCollectionSettings.GetParameters(); }
        }

        public static IEnumerable<ParameterInfo[]> ValidCosmosDBTriggerBindingsDifferentConnectionsParameters
        {
            get { return ValidCosmosDBTriggerBindigsDifferentConnections.GetParameters(); }
        }

        public static IEnumerable<ParameterInfo[]> ValidCosmosDBTriggerBindingsWithEnvironmentParameters
        {
            get { return ValidCosmosDBTriggerBindigsWithEnvironment.GetParameters(); }
        }

        public static IEnumerable<ParameterInfo[]> ValidCosmosDBTriggerBindigsWithLeaseHostOptionsParameters
        {
            get { return ValidCosmosDBTriggerBindigsWithLeaseHostOptions.GetParameters(); }
        }

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindigsWithChangeFeedOptionsParameters
        {
            get { return ValidCosmosDBTriggerBindigsWithChangeFeedOptions.GetParameters(); }
        }

        private static ParameterInfo GetFirstParameter(Type type, string methodName)
        {
            var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            var paramInfo = methodInfo.GetParameters().First();

            return paramInfo;
        }

        private static class InvalidCosmosDBTriggerBindigs
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
                var type = typeof(InvalidCosmosDBTriggerBindigs);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") },
                    new[] { GetFirstParameter(type, "Func4") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsWithAppSettings
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
                var type = typeof(ValidCosmosDBTriggerBindigsWithAppSettings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigsWithLeaseHostOptions
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", LeaseCollectionPrefix = "someLeasePrefix")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", LeaseCollectionPrefix = "%dynamicLeasePrefix%")] IReadOnlyList<Document> docs)
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

        private static class ValidCosmosDBTriggerBindigsWithChangeFeedOptions
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "CosmosDBConnectionString", MaxItemsPerInvocation = 10, StartFromBeginning = true)] IReadOnlyList<Document> docs)
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

        private DocumentDBConfiguration CreateConfiguration()
        {
            return new DocumentDBConfiguration
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=c29tZV9rZXk=;",
            };
        }

        private DocumentDBConfiguration CreateConfigurationWithConnectionMode()
        {
            return new DocumentDBConfiguration
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=c29tZV9rZXk=;",
                ConnectionMode = ConnectionMode.Direct,
                Protocol = Protocol.Tcp
            };
        }
    }
}