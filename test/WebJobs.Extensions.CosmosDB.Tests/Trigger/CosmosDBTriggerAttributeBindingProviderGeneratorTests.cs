// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBTrigger.Tests
{
    /// <summary>
    /// Targets the AVAD type-shim behavior of <see cref="CosmosDBTriggerAttributeBindingProviderGenerator"/>:
    /// when <see cref="CosmosDBTriggerAttribute.ChangeFeedMode"/> is AllVersionsAndDeletes, the resolved
    /// document type used to construct <see cref="CosmosDBTriggerBinding{T}"/> must be wrapped in
    /// <see cref="ChangeFeedItem{T}"/> so that the trigger value type the host advertises matches what
    /// the change feed processor actually delivers.
    /// </summary>
    public class CosmosDBTriggerAttributeBindingProviderGeneratorTests
    {
        private readonly ILoggerFactory _loggerFactory = new LoggerFactory();
        private readonly IDrainModeManager _drainModeManager = Mock.Of<IDrainModeManager>();
        private readonly CosmosDBOptions _options = new CosmosDBOptions();
        private static readonly IConfiguration _baseConfig = CosmosDBTestUtility.BuildConfiguration(
        [
            Tuple.Create(Constants.DefaultConnectionStringName, "AccountEndpoint=https://fromEnvironment;AccountKey=c29tZV9rZXk=;")
        ]);

        [Fact]
        public async Task TryCreateAsync_LatestVersion_JArray_ProducesJObjectBinding()
        {
            // Baseline: existing behavior - JArray parameter resolves to CosmosDBTriggerBinding<JObject>.
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.JArrayLatestVersion));

            ITriggerBinding binding = await CreateGenerator().TryCreateAsync(
                new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.IsType<CosmosDBTriggerBinding<JObject>>(binding);
            Assert.Equal(typeof(IReadOnlyCollection<JObject>), binding.TriggerValueType);
        }

        [Fact]
        public async Task TryCreateAsync_LatestVersion_StronglyTyped_ProducesUserTypeBinding()
        {
            // Baseline: existing behavior - typed parameter resolves to CosmosDBTriggerBinding<MyDocument>.
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.TypedLatestVersion));

            ITriggerBinding binding = await CreateGenerator().TryCreateAsync(
                new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.IsType<CosmosDBTriggerBinding<MyDocument>>(binding);
            Assert.Equal(typeof(IReadOnlyCollection<MyDocument>), binding.TriggerValueType);
        }

        [Fact]
        public async Task TryCreateAsync_AllVersionsAndDeletes_JArray_WrapsInChangeFeedItem()
        {
            // The AVAD shim: a JArray-bound parameter under AllVersionsAndDeletes mode must produce
            // CosmosDBTriggerBinding<ChangeFeedItem<JObject>> so the value flowing through the host
            // matches what GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes emits.
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.JArrayAllVersionsAndDeletes));

            ITriggerBinding binding = await CreateGenerator().TryCreateAsync(
                new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.IsType<CosmosDBTriggerBinding<ChangeFeedItem<JObject>>>(binding);
            Assert.Equal(typeof(IReadOnlyCollection<ChangeFeedItem<JObject>>), binding.TriggerValueType);
        }

        [Fact]
        public async Task TryCreateAsync_AllVersionsAndDeletes_String_WrapsInChangeFeedItem()
        {
            // String-bound parameter under AllVersionsAndDeletes mode follows the same shimming
            // path as JArray (the generator collapses string -> JObject before the AVAD wrap).
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.StringAllVersionsAndDeletes));

            ITriggerBinding binding = await CreateGenerator().TryCreateAsync(
                new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.IsType<CosmosDBTriggerBinding<ChangeFeedItem<JObject>>>(binding);
            Assert.Equal(typeof(IReadOnlyCollection<ChangeFeedItem<JObject>>), binding.TriggerValueType);
        }

        [Fact]
        public async Task TryCreateAsync_AllVersionsAndDeletes_AlreadyChangeFeedItem_DoesNotDoubleWrap()
        {
            // If the user already binds to IReadOnlyList<ChangeFeedItem<MyDocument>>, the shim must
            // detect the existing wrapper and produce CosmosDBTriggerBinding<ChangeFeedItem<MyDocument>>
            // - NOT CosmosDBTriggerBinding<ChangeFeedItem<ChangeFeedItem<MyDocument>>>.
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.TypedChangeFeedItemAllVersionsAndDeletes));

            ITriggerBinding binding = await CreateGenerator().TryCreateAsync(
                new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.IsType<CosmosDBTriggerBinding<ChangeFeedItem<MyDocument>>>(binding);
            Assert.Equal(typeof(IReadOnlyCollection<ChangeFeedItem<MyDocument>>), binding.TriggerValueType);
        }

        [Fact]
        public async Task TryCreateAsync_AllVersionsAndDeletes_StronglyTyped_DoesNotWrap()
        {
            // A user-defined POCO (e.g. IReadOnlyList<MyDocument>) under AVAD mode is treated as
            // an in-proc user binding and is intentionally NOT wrapped. T flows through to the
            // listener as MyDocument, where the listener's type guard surfaces a clear
            // InvalidOperationException ("must be ChangeFeedItem<T>") - see
            // CosmosDBListenerTests.StartAsync_AllVersionsAndDeletes_NonChangeFeedItemType_Throws.
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.TypedAllVersionsAndDeletes));

            ITriggerBinding binding = await CreateGenerator().TryCreateAsync(
                new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.IsType<CosmosDBTriggerBinding<MyDocument>>(binding);
            Assert.Equal(typeof(IReadOnlyCollection<MyDocument>), binding.TriggerValueType);
        }

        [Fact]
        public async Task TryCreateAsync_NoAttribute_ReturnsNull()
        {
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.NoTriggerAttribute));

            ITriggerBinding binding = await CreateGenerator().TryCreateAsync(
                new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Null(binding);
        }

        private CosmosDBTriggerAttributeBindingProviderGenerator CreateGenerator()
        {
            return new CosmosDBTriggerAttributeBindingProviderGenerator(
                new TestNameResolver(),
                _options,
                CreateExtensionConfigProvider(_options, _baseConfig),
                _drainModeManager,
                _loggerFactory);
        }

        private static CosmosDBExtensionConfigProvider CreateExtensionConfigProvider(CosmosDBOptions options, IConfiguration config)
        {
            return new CosmosDBExtensionConfigProvider(
                new OptionsWrapper<CosmosDBOptions>(options),
                new DefaultCosmosDBServiceFactory(config, Mock.Of<AzureComponentFactory>()),
                new DefaultCosmosDBSerializerFactory(),
                new TestNameResolver(),
                Mock.Of<IDrainModeManager>(),
                NullLoggerFactory.Instance);
        }

        private static ParameterInfo GetFirstParameter(string methodName)
        {
            MethodInfo method = typeof(SampleFunctions).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            return method.GetParameters().First();
        }

        public class MyDocument
        {
            public string Id { get; set; }
        }

        private static class SampleFunctions
        {
            public static void JArrayLatestVersion(
                [CosmosDBTrigger("aDatabase", "aCollection")] JArray docs)
            {
            }

            public static void JArrayAllVersionsAndDeletes(
                [CosmosDBTrigger("aDatabase", "aCollection", ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes)] JArray docs)
            {
            }

            public static void StringAllVersionsAndDeletes(
                [CosmosDBTrigger("aDatabase", "aCollection", ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes)] string docs)
            {
            }

            public static void TypedLatestVersion(
                [CosmosDBTrigger("aDatabase", "aCollection")] IReadOnlyList<MyDocument> docs)
            {
            }

            public static void TypedAllVersionsAndDeletes(
                [CosmosDBTrigger("aDatabase", "aCollection", ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes)] IReadOnlyList<MyDocument> docs)
            {
            }

            public static void TypedChangeFeedItemAllVersionsAndDeletes(
                [CosmosDBTrigger("aDatabase", "aCollection", ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes)] IReadOnlyList<ChangeFeedItem<MyDocument>> docs)
            {
            }

            public static void NoTriggerAttribute(string docs)
            {
            }
        }
    }
}
