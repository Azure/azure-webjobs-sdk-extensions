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
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
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

        public static IEnumerable<object[]> ValidCosmosDBTriggerBindingsCreateLeaseContainerParameters
        {
            get { return ValidCosmosDBTriggerBindingsCreateLeaseContainer.GetParameters(); }
        }

        [Theory]
        [MemberData(nameof(InvalidCosmosDBTriggerParameters))]
        public async Task InvalidParameters_Fail(ParameterInfo parameter)
        {
            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(_emptyConfig, new TestNameResolver(), _options, CreateExtensionConfigProvider(_options), _loggerFactory);

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

            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding<dynamic> binding = (CosmosDBTriggerBinding<dynamic>)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyCollection<dynamic>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.MonitoredContainer.Database.Client.Endpoint);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.LeaseContainer.Database.Client.Endpoint);
            Assert.Null(binding.MonitoredContainer.Database.Client.ClientOptions.ApplicationPreferredRegions);
            Assert.Null(binding.LeaseContainer.Database.Client.ClientOptions.ApplicationPreferredRegions);
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

            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding<dynamic> binding = (CosmosDBTriggerBinding<dynamic>)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyCollection<dynamic>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromSettings"), binding.MonitoredContainer.Database.Client.Endpoint);
            Assert.Equal(new Uri("https://fromSettings"), binding.LeaseContainer.Database.Client.Endpoint);
            Assert.Equal("myDatabase", binding.MonitoredContainer.Database.Id);
            Assert.Equal("myCollection", binding.MonitoredContainer.Id);
            Assert.Equal("myDatabase", binding.LeaseContainer.Database.Id);
            Assert.Equal("leases", binding.LeaseContainer.Id);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsWithDatabaseAndCollectionSettingsParameters))]
        public async Task ValidCosmosDBTriggerBindigsWithDatabaseAndCollectionSettings_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values["CosmosDBConnectionString"] = "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;";
            nameResolver.Values["aDatabase"] = "myDatabase";
            nameResolver.Values["aCollection"] = "myCollection";

            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(_emptyConfig, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding<dynamic> binding = (CosmosDBTriggerBinding<dynamic>)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyCollection<dynamic>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.MonitoredContainer.Database.Client.Endpoint);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.LeaseContainer.Database.Client.Endpoint);
            Assert.Equal("myDatabase-test", binding.MonitoredContainer.Database.Id);
            Assert.Equal("myCollection-test", binding.MonitoredContainer.Id);
            Assert.Equal("myDatabase-test", binding.LeaseContainer.Database.Id);
            Assert.Equal("leases", binding.LeaseContainer.Id);
            Assert.Equal(string.Empty, binding.ProcessorName);
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

            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding<dynamic> binding = (CosmosDBTriggerBinding<dynamic>)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyCollection<dynamic>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromSettings"), binding.MonitoredContainer.Database.Client.Endpoint);
            Assert.Equal(new Uri("https://fromSettingsLease"), binding.LeaseContainer.Database.Client.Endpoint);
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

            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding<dynamic> binding = (CosmosDBTriggerBinding<dynamic>)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(typeof(IReadOnlyCollection<dynamic>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.MonitoredContainer.Database.Client.Endpoint);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.LeaseContainer.Database.Client.Endpoint);
            Assert.Equal(ConnectionMode.Direct, binding.LeaseContainer.Database.Client.ClientOptions.ConnectionMode);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindigsPreferredLocationsParameters))]
        public async Task ValidCosmosDBTriggerBindigsPreferredLocationsParameters_Succeed(ParameterInfo parameter)
        {
            var nameResolver = new TestNameResolver();
            nameResolver.Values["regions"] = "East US, North Europe,";

            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(_emptyConfig, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding<dynamic> binding = (CosmosDBTriggerBinding<dynamic>)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(2, binding.MonitoredContainer.Database.Client.ClientOptions.ApplicationPreferredRegions.Count);
            Assert.Equal(2, binding.LeaseContainer.Database.Client.ClientOptions.ApplicationPreferredRegions.Count);
            Assert.Equal("East US", binding.MonitoredContainer.Database.Client.ClientOptions.ApplicationPreferredRegions[0]);
            Assert.Equal("North Europe", binding.MonitoredContainer.Database.Client.ClientOptions.ApplicationPreferredRegions[1]);
            Assert.Equal("East US", binding.LeaseContainer.Database.Client.ClientOptions.ApplicationPreferredRegions[0]);
            Assert.Equal("North Europe", binding.LeaseContainer.Database.Client.ClientOptions.ApplicationPreferredRegions[1]);
        }

        [Fact]
        public void ResolveTimeSpanFromMilliseconds_Succeed()
        {
            TimeSpan baseTimeSpan = TimeSpan.FromMilliseconds(10);
            Assert.Equal(CosmosDBTriggerAttributeBindingProvider<dynamic>.ResolveTimeSpanFromMilliseconds("SomeAttribute", baseTimeSpan, null), baseTimeSpan);
            int otherMilliseconds = 20;
            TimeSpan otherTimeSpan = TimeSpan.FromMilliseconds(otherMilliseconds);
            Assert.Equal(CosmosDBTriggerAttributeBindingProvider<dynamic>.ResolveTimeSpanFromMilliseconds("SomeAttribute", baseTimeSpan, otherMilliseconds), otherTimeSpan);
        }

        [Fact]
        public void ResolveTimeSpanFromMilliseconds_Fail()
        {
            int otherMilliseconds = -1;
            TimeSpan baseTimeSpan = TimeSpan.FromMilliseconds(10);
            Assert.Throws<InvalidOperationException>(() => CosmosDBTriggerAttributeBindingProvider<dynamic>.ResolveTimeSpanFromMilliseconds("SomeAttribute", baseTimeSpan, otherMilliseconds));
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

            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(config, nameResolver, _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding<dynamic> binding = (CosmosDBTriggerBinding<dynamic>)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            // This test uses the default for ConnectionStringSetting, but overrides LeaseConnectionStringSetting
            Assert.Equal(typeof(IReadOnlyCollection<dynamic>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromEnvironment"), binding.MonitoredContainer.Database.Client.Endpoint);
            Assert.Equal(new Uri("https://overridden"), binding.LeaseContainer.Database.Client.Endpoint);
            Assert.Equal("someLeasePrefix", binding.ProcessorName);
            Assert.False(binding.CosmosDBAttribute.StartFromBeginning);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsCreateLeaseContainerParameters))]
        public async Task ValidCreateIfNotExists(ParameterInfo parameter)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    // Verify we load from root config
                    { "CosmosDBConnectionString", "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;" }
                })
                .Build();

            var serviceMock = new Mock<CosmosClient>(MockBehavior.Strict);

            var mockDatabase = CosmosDBTestUtility.SetupDatabaseMock(serviceMock);

            serviceMock
                .Setup(m => m.GetDatabase(It.IsAny<string>()))
                .Returns(mockDatabase.Object);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            serviceMock
                .Setup(m => m.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockContainer.Object);

            mockDatabase
                .Setup(m => m.GetContainer(It.IsAny<string>()))
                .Returns(mockContainer.Object);

            // Forcing creation of the container because it does not exist
            mockContainer
                .Setup(m => m.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("not found", System.Net.HttpStatusCode.NotFound, 0, Guid.NewGuid().ToString(), 0));

            mockDatabase
                .Setup(m => m.CreateContainerAsync(It.IsAny<string>(), It.Is<string>(pk => pk == "/id"), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ContainerResponse>());

            var factoryMock = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(It.IsAny<string>(), It.IsAny<CosmosClientOptions>()))
                .Returns(serviceMock.Object);

            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(config, new TestNameResolver(), _options, CreateExtensionConfigProvider(factoryMock.Object, _options), _loggerFactory);

            CosmosDBTriggerBinding<dynamic> binding = (CosmosDBTriggerBinding<dynamic>)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            CosmosDBTriggerAttribute cosmosDBTriggerAttribute = parameter.GetCustomAttribute<CosmosDBTriggerAttribute>(inherit: false);

            mockDatabase
                .Verify(m => m.CreateContainerAsync(It.IsAny<string>(), It.Is<string>(pk => pk == "/id"), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [MemberData(nameof(ValidCosmosDBTriggerBindingsCreateLeaseContainerParameters))]
        public async Task ValidCreateIfNotExistsForGremlin(ParameterInfo parameter)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    // Verify we load from root config
                    { "CosmosDBConnectionString", "AccountEndpoint=https://fromSettings;AccountKey=c29tZV9rZXk=;" }
                })
                .Build();

            var serviceMock = new Mock<CosmosClient>(MockBehavior.Strict);

            var mockDatabase = CosmosDBTestUtility.SetupDatabaseMock(serviceMock);

            serviceMock
                .Setup(m => m.GetDatabase(It.IsAny<string>()))
                .Returns(mockDatabase.Object);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            serviceMock
                .Setup(m => m.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(mockContainer.Object);

            mockDatabase
                .Setup(m => m.GetContainer(It.IsAny<string>()))
                .Returns(mockContainer.Object);

            // Forcing creation of the container because it does not exist
            mockContainer
                .Setup(m => m.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("not found", System.Net.HttpStatusCode.NotFound, 0, Guid.NewGuid().ToString(), 0));

            mockDatabase
                .Setup(m => m.CreateContainerAsync(It.IsAny<string>(), It.Is<string>(pk => pk == "/id"), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("invalid for Gremlin API", System.Net.HttpStatusCode.BadRequest, 0, Guid.NewGuid().ToString(), 0));

            mockDatabase
                .Setup(m => m.CreateContainerAsync(It.IsAny<string>(), It.Is<string>(pk => pk == "/partitionKey"), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<ContainerResponse>());

            var factoryMock = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(It.IsAny<string>(), It.IsAny<CosmosClientOptions>()))
                .Returns(serviceMock.Object);

            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(config, new TestNameResolver(), _options, CreateExtensionConfigProvider(factoryMock.Object, _options), _loggerFactory);

            CosmosDBTriggerBinding<dynamic> binding = (CosmosDBTriggerBinding<dynamic>)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            CosmosDBTriggerAttribute cosmosDBTriggerAttribute = parameter.GetCustomAttribute<CosmosDBTriggerAttribute>(inherit: false);

            mockDatabase
                .Verify(m => m.CreateContainerAsync(It.IsAny<string>(), It.Is<string>(pk => pk == "/id"), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);

            mockDatabase
                .Verify(m => m.CreateContainerAsync(It.IsAny<string>(), It.Is<string>(pk => pk == "/partitionKey"), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
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

            CosmosDBTriggerAttributeBindingProvider<dynamic> provider = new CosmosDBTriggerAttributeBindingProvider<dynamic>(config, new TestNameResolver(), _options, CreateExtensionConfigProvider(_options), _loggerFactory);

            CosmosDBTriggerBinding<dynamic> binding = (CosmosDBTriggerBinding<dynamic>)await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            CosmosDBTriggerAttribute cosmosDBTriggerAttribute = parameter.GetCustomAttribute<CosmosDBTriggerAttribute>(inherit: false);

            Assert.Equal(typeof(IReadOnlyCollection<dynamic>), binding.TriggerValueType);
            Assert.Equal(new Uri("https://fromSettings"), binding.MonitoredContainer.Database.Client.Endpoint);
            Assert.Equal(new Uri("https://fromSettings"), binding.LeaseContainer.Database.Client.Endpoint);
        }

        private static ParameterInfo GetFirstParameter(Type type, string methodName)
        {
            var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            var paramInfo = methodInfo.GetParameters().First();

            return paramInfo;
        }

        private static CosmosDBExtensionConfigProvider CreateExtensionConfigProvider(CosmosDBOptions options)
        {
            return CreateExtensionConfigProvider(new DefaultCosmosDBServiceFactory(), options);
        }

        private static CosmosDBExtensionConfigProvider CreateExtensionConfigProvider(ICosmosDBServiceFactory serviceFactory, CosmosDBOptions options)
        {
            return new CosmosDBExtensionConfigProvider(new OptionsWrapper<CosmosDBOptions>(options), serviceFactory, new DefaultCosmosDBSerializerFactory(), _emptyConfig, new TestNameResolver(), NullLoggerFactory.Instance);
        }

        // These will use the default for ConnectionStringSetting, but override LeaseConnectionStringSetting
        private static class ValidCosmosDBTriggerBindigsWithLeaseHostOptions
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", LeaseConnection = "LeaseConnectionString", LeaseContainerPrefix = "someLeasePrefix")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", LeaseConnection = "LeaseConnectionString", LeaseContainerPrefix = "%dynamicLeasePrefix%")] IReadOnlyList<dynamic> docs)
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
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", Connection = "CosmosDBConnectionString", MaxItemsPerInvocation = 10, StartFromBeginning = true)] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", Connection = "CosmosDBConnectionString", MaxItemsPerInvocation = 10, StartFromTime = "2020-11-25T22:36:29Z")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func3([CosmosDBTrigger("aDatabase", "aCollection", Connection = "CosmosDBConnectionString", MaxItemsPerInvocation = 10, StartFromBeginning = false, StartFromTime = "2020-11-25T22:36:29Z")] IReadOnlyList<dynamic> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsWithChangeFeedOptions);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") }
                };
            }
        }

        private static class InvalidCosmosDBTriggerBindings
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", Connection = "notAConnectionString")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", Connection = "notAConnectionString", LeaseConnection = "notAConnectionString", LeaseDatabaseName = "aDatabase", LeaseContainerName = "aCollection")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func3([CosmosDBTrigger("aDatabase", "aCollection", Connection = "CosmosDBConnectionString", LeaseConnection = "CosmosDBConnectionString", LeaseDatabaseName = "aDatabase", LeaseContainerName = "aCollection")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func4([CosmosDBTrigger("aDatabase", "leases", Connection = "CosmosDBConnectionString")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func5([CosmosDBTrigger("aDatabase", "leases", Connection = "CosmosDBConnectionString", StartFromBeginning = true, StartFromTime = "2020-11-25T22:36:29Z")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func6([CosmosDBTrigger("aDatabase", "leases", Connection = "CosmosDBConnectionString", StartFromTime = "blah")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func7([CosmosDBTrigger("aDatabase", "leases", Connection = "CosmosDBConnectionString", StartFromBeginning = true, StartFromTime = "blah")] IReadOnlyList<dynamic> docs)
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
                    new[] { GetFirstParameter(type, "Func4") },
                    new[] { GetFirstParameter(type, "Func5") },
                    new[] { GetFirstParameter(type, "Func6") },
                    new[] { GetFirstParameter(type, "Func7") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindingsWithAppSettings
        {
            public static void Func1([CosmosDBTrigger("%aDatabase%", "%aCollection%", Connection = "CosmosDBConnectionString")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("%aDatabase%", "%aCollection%", Connection = "CosmosDBConnectionString", LeaseConnection = "CosmosDBConnectionString", LeaseDatabaseName = "%aDatabase%")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func3([CosmosDBTrigger("%aDatabase%", "%aCollection%", Connection = "CosmosDBConnectionString")] JArray docs)
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
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", Connection = "CosmosDBConnectionString", LeaseConnection = "LeaseCosmosDBConnectionString")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", Connection = "CosmosDBConnectionString", LeaseConnection = "LeaseCosmosDBConnectionString", LeaseDatabaseName = "aDatabase", LeaseContainerName = "aLeaseCollection")] IReadOnlyList<dynamic> docs)
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
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection")] JArray docs)
            {
            }

            public static void Func3([CosmosDBTrigger("aDatabase", "aCollection", LeaseDatabaseName = "aDatabase", LeaseContainerName = "aLeaseCollection")] IReadOnlyList<dynamic> docs)
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
            public static void Func1([CosmosDBTrigger("%aDatabase%-test", "%aCollection%-test")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("%aDatabase%-test", "%aCollection%-test")] JArray docs)
            {
            }

            public static void Func3([CosmosDBTrigger("%aDatabase%-test", "%aCollection%-test", LeaseDatabaseName = "%aDatabase%-test")] IReadOnlyList<dynamic> docs)
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
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", PreferredLocations = "East US, North Europe,")] IReadOnlyList<dynamic> docs)
            {
            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", PreferredLocations = "%regions%")] IReadOnlyList<dynamic> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigsPreferredLocations);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindingsCreateLeaseContainer
        {
            public static void Func1([CosmosDBTrigger("ItemDB", "ItemCollection", CreateLeaseContainerIfNotExists = true)] IReadOnlyList<dynamic> docs)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindingsCreateLeaseContainer);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") }
                };
            }
        }
    }
}