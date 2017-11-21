// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Moq;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    internal static class DocumentDBTestUtility
    {
        public const string DatabaseName = "ItemDB";
        public const string CollectionName = "ItemCollection";

        public static void SetupCollectionMock(Mock<IDocumentDBService> mockService, string partitionKeyPath = null, int throughput = 400)
        {
            Uri databaseUri = UriFactory.CreateDatabaseUri(DatabaseName);

            var expectedPaths = new List<string>();
            if (!string.IsNullOrEmpty(partitionKeyPath))
            {
                expectedPaths.Add(partitionKeyPath);
            }

            mockService
                    .Setup(m => m.CreateDocumentCollectionIfNotExistsAsync(databaseUri,
                        It.Is<DocumentCollection>(d => d.Id == CollectionName && Enumerable.SequenceEqual(d.PartitionKey.Paths, expectedPaths)),
                        It.Is<RequestOptions>(r => r.OfferThroughput == throughput)))
                    .ReturnsAsync(new DocumentCollection());
        }

        public static void SetupDatabaseMock(Mock<IDocumentDBService> mockService)
        {
            mockService
                .Setup(m => m.CreateDatabaseIfNotExistsAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ReturnsAsync(new Database());
        }

        public static DocumentDBContext CreateContext(IDocumentDBService service, bool createIfNotExists = false,
            string partitionKeyPath = null, int throughput = 0)
        {
            DocumentDBAttribute attribute = new DocumentDBAttribute(DocumentDBTestUtility.DatabaseName, DocumentDBTestUtility.CollectionName)
            {
                CreateIfNotExists = createIfNotExists,
                PartitionKey = partitionKeyPath,
                CollectionThroughput = throughput
            };

            return new DocumentDBContext
            {
                Service = service,
                ResolvedAttribute = attribute
            };
        }

        public static IOrderedQueryable<T> AsOrderedQueryable<T, TKey>(this IEnumerable<T> enumerable, Expression<Func<T, TKey>> keySelector)
        {
            return enumerable.AsQueryable().OrderBy(keySelector);
        }

        public static DocumentClientException CreateDocumentClientException(HttpStatusCode status)
        {
            var headers = new NameValueCollection();
            var parameters = new object[] { null, null, headers, status, null };
            return Activator.CreateInstance(typeof(DocumentClientException), BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null) as DocumentClientException;
        }

        public static ParameterInfo GetInputParameter<T>()
        {
            return GetValidItemInputParameters().Where(p => p.ParameterType == typeof(T)).Single();
        }

        public static IEnumerable<ParameterInfo> GetCreateIfNotExistsParameters()
        {
            return typeof(DocumentDBTestUtility)
                .GetMethod("CreateIfNotExistsParameters", BindingFlags.Static | BindingFlags.NonPublic).GetParameters();
        }

        public static IEnumerable<ParameterInfo> GetValidOutputParameters()
        {
            return typeof(DocumentDBTestUtility)
                .GetMethod("OutputParameters", BindingFlags.Static | BindingFlags.NonPublic).GetParameters();
        }

        public static IEnumerable<ParameterInfo> GetValidItemInputParameters()
        {
            return typeof(DocumentDBTestUtility)
                 .GetMethod("ItemInputParameters", BindingFlags.Static | BindingFlags.NonPublic).GetParameters();
        }

        public static IEnumerable<ParameterInfo> GetValidClientInputParameters()
        {
            return typeof(DocumentDBTestUtility)
                 .GetMethod("ClientInputParameters", BindingFlags.Static | BindingFlags.NonPublic).GetParameters();
        }

        public static Type GetAsyncCollectorType(Type itemType)
        {
            Assembly hostAssembly = typeof(BindingFactory).Assembly;
            return hostAssembly.GetType("Microsoft.Azure.WebJobs.Host.Bindings.AsyncCollectorBinding`2")
                .MakeGenericType(itemType, typeof(DocumentDBContext));
        }

        private static void CreateIfNotExistsParameters(
            [DocumentDB("TestDB", "TestCollection", CreateIfNotExists = false)] out Item pocoOut,
            [DocumentDB("TestDB", "TestCollection", CreateIfNotExists = true)] out Item[] pocoArrayOut)
        {
            pocoOut = null;
            pocoArrayOut = null;
        }

        private static void OutputParameters(
            [DocumentDB] out object pocoOut,
            [DocumentDB] out Item[] pocoArrayOut,
            [DocumentDB] IAsyncCollector<JObject> jobjectAsyncCollector,
            [DocumentDB] ICollector<Item> pocoCollector,
            [DocumentDB] out IAsyncCollector<object> collectorOut)
        {
            pocoOut = null;
            pocoArrayOut = null;
            collectorOut = null;
        }

        private static void ItemInputParameters(
            [DocumentDB(Id = "abc123")] Document document,
            [DocumentDB(Id = "abc123")] Item poco,
            [DocumentDB(Id = "abc123")] object obj)
        {
        }

        private static void ClientInputParameters(
            [DocumentDB] DocumentClient client)
        {
        }
    }
}
