// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Threading;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    internal static class CosmosDBTestUtility
    {
        public const string DatabaseName = "ItemDB";
        public const string ContainerName = "ItemCollection";

        public static Mock<Container> SetupCollectionMock(Mock<CosmosClient> mockService, Mock<Database> mockDatabase, string partitionKeyPath, int throughput = 0, bool setTTL = false)
        {
            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
               .Setup(m => m.GetDatabase(It.Is<string>(d => d == DatabaseName)))
               .Returns(mockDatabase.Object);

            var response = new Mock<ContainerResponse>();
            response
                .Setup(m => m.Container)
                .Returns(mockContainer.Object);

            mockDatabase
                .Setup(db => db.GetContainer(It.Is<string>(i => i == ContainerName)))
                .Returns(mockContainer.Object);

            mockContainer
                .Setup(c => c.ReadContainerAsync(It.IsAny<ContainerRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("test", HttpStatusCode.NotFound, 0, string.Empty, 0));

            if (throughput == 0)
            {
                mockDatabase
                    .Setup(m => m.CreateContainerAsync(It.Is<ContainerProperties>(cp => cp.Id == ContainerName && cp.PartitionKeyPath == partitionKeyPath && (!setTTL || cp.DefaultTimeToLive == -1)),
                        It.Is<int?>(t => t == null),
                        It.IsAny<RequestOptions>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(response.Object);
            }
            else
            {
                mockDatabase
                    .Setup(m => m.CreateContainerAsync(It.Is<ContainerProperties>(cp => cp.Id == ContainerName && cp.PartitionKeyPath == partitionKeyPath && (!setTTL || cp.DefaultTimeToLive == -1)),
                        It.Is<int?>(t => t == throughput),
                        It.IsAny<RequestOptions>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(response.Object);
            }

            return mockContainer;
        }

        public static Mock<Database> SetupDatabaseMock(Mock<CosmosClient> mockService)
        {
            Mock<Database> database = new Mock<Database>();
            database
                .Setup(m => m.Id)
                .Returns(DatabaseName);

            Mock<DatabaseResponse> response = new Mock<DatabaseResponse>();
            response
                .Setup(m => m.Database)
                .Returns(database.Object);

            mockService
                .Setup(m => m.CreateDatabaseIfNotExistsAsync(It.Is<string>(d => d == DatabaseName), It.IsAny<int?>(), It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response.Object);

            return database;
        }

        public static CosmosDBContext CreateContext(CosmosClient service, bool createIfNotExists = false,
            string partitionKeyPath = null, int throughput = 0)
        {
            CosmosDBAttribute attribute = new CosmosDBAttribute(CosmosDBTestUtility.DatabaseName, CosmosDBTestUtility.ContainerName)
            {
                CreateIfNotExists = createIfNotExists,
                PartitionKey = partitionKeyPath,
                ContainerThroughput = throughput
            };

            return new CosmosDBContext
            {
                Service = service,
                ResolvedAttribute = attribute
            };
        }

        public static IConfiguration BuildConfiguration(List<Tuple<string,string>> configs)
        {
            var mock = new Mock<IConfiguration>();
            foreach (var config in configs)
            {
                var section = new Mock<IConfigurationSection>();
                section.Setup(s => s.Value).Returns(config.Item2);
                mock.Setup(c => c.GetSection(It.Is<string>(sectionName => sectionName == config.Item1))).Returns(section.Object);
            }

            return mock.Object;
        }

        public static IOrderedQueryable<T> AsOrderedQueryable<T, TKey>(this IEnumerable<T> enumerable, Expression<Func<T, TKey>> keySelector)
        {
            return enumerable.AsQueryable().OrderBy(keySelector);
        }

        public static CosmosException CreateDocumentClientException(HttpStatusCode status)
        {
            return new CosmosException("error!", status, 0, string.Empty, 0);
        }

        public static ParameterInfo GetInputParameter<T>()
        {
            return GetValidItemInputParameters().Where(p => p.ParameterType == typeof(T)).Single();
        }

        public static IEnumerable<ParameterInfo> GetCreateIfNotExistsParameters()
        {
            return typeof(CosmosDBTestUtility)
                .GetMethod("CreateIfNotExistsParameters", BindingFlags.Static | BindingFlags.NonPublic).GetParameters();
        }

        public static IEnumerable<ParameterInfo> GetValidOutputParameters()
        {
            return typeof(CosmosDBTestUtility)
                .GetMethod("OutputParameters", BindingFlags.Static | BindingFlags.NonPublic).GetParameters();
        }

        public static IEnumerable<ParameterInfo> GetValidItemInputParameters()
        {
            return typeof(CosmosDBTestUtility)
                 .GetMethod("ItemInputParameters", BindingFlags.Static | BindingFlags.NonPublic).GetParameters();
        }

        public static IEnumerable<ParameterInfo> GetValidClientInputParameters()
        {
            return typeof(CosmosDBTestUtility)
                 .GetMethod("ClientInputParameters", BindingFlags.Static | BindingFlags.NonPublic).GetParameters();
        }

        public static Type GetAsyncCollectorType(Type itemType)
        {
            Assembly hostAssembly = typeof(BindingFactory).Assembly;
            return hostAssembly.GetType("Microsoft.Azure.WebJobs.Host.Bindings.AsyncCollectorBinding`2")
                .MakeGenericType(itemType, typeof(CosmosDBContext));
        }

        private static void CreateIfNotExistsParameters(
            [CosmosDB("TestDB", "TestCollection", CreateIfNotExists = false)] out Item pocoOut,
            [CosmosDB("TestDB", "TestCollection", CreateIfNotExists = true)] out Item[] pocoArrayOut)
        {
            pocoOut = null;
            pocoArrayOut = null;
        }

        private static void OutputParameters(
            [CosmosDB] out object pocoOut,
            [CosmosDB] out Item[] pocoArrayOut,
            [CosmosDB] IAsyncCollector<JObject> jobjectAsyncCollector,
            [CosmosDB] ICollector<Item> pocoCollector,
            [CosmosDB] out IAsyncCollector<object> collectorOut)
        {
            pocoOut = null;
            pocoArrayOut = null;
            collectorOut = null;
        }

        private static void ItemInputParameters(
            [CosmosDB(Id = "abc123")] Item poco,
            [CosmosDB(Id = "abc123")] object obj)
        {
        }

        private static void ClientInputParameters(
            [CosmosDB] CosmosClient client)
        {
        }
    }
}
