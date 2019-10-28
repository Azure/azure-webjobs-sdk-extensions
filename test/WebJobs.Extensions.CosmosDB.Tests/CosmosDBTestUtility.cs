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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    internal static class CosmosDBTestUtility
    {
        public const string DatabaseName = "ItemDB";
        public const string ContainerName = "ItemCollection";

        // Runs the standard options pipeline for initialization
        public static IOptions<CosmosDBOptions> InitializeOptions(string defaultConnectionString, string optionsConnectionString)
        {
            // Create the Options like we do during host build
            var builder = new HostBuilder()               
                .ConfigureWebJobs(b =>
                {
                    // This wires up our options pipeline.
                    b.AddCosmosDB();

                    // If someone is updating the ConnectionString, they'd do it like this.
                    if (optionsConnectionString != null)
                    {
                        b.Services.Configure<CosmosDBOptions>(o =>
                        {
                            o.ConnectionString = optionsConnectionString;
                        });
                    }
                })
                .ConfigureAppConfiguration(b =>
                {
                    b.Sources.Clear();
                    if (defaultConnectionString != null)
                    {
                        b.AddInMemoryCollection(new Dictionary<string, string>
                        {
                            { $"ConnectionStrings:{Constants.DefaultConnectionStringName}", defaultConnectionString }
                        });
                    }
                });

            return builder.Build().Services.GetService<IOptions<CosmosDBOptions>>();
        }

        public static Mock<Container> SetupCollectionMock(Mock<CosmosClient> mockService, Mock<Database> mockDatabase, string partitionKeyPath = null, int throughput = 0)
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
                    .Setup(m => m.CreateContainerAsync(It.Is<string>(i => i == ContainerName),
                        It.Is<string>(p => p == partitionKeyPath),
                        It.Is<int?>(t => t == null),
                        It.IsAny<RequestOptions>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(response.Object);
            }
            else
            {
                mockDatabase
                    .Setup(m => m.CreateContainerAsync(It.Is<string>(i => i == ContainerName),
                        It.Is<string>(p => p == partitionKeyPath),
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
                CollectionThroughput = throughput
            };

            return new CosmosDBContext
            {
                Service = service,
                ResolvedAttribute = attribute
            };
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
