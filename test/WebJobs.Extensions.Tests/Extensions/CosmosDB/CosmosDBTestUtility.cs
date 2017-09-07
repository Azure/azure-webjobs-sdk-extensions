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
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB
{
    internal static class CosmosDBTestUtility
    {
        public static IOrderedQueryable<T> AsOrderedQueryable<T, TKey>(this IEnumerable<T> enumerable, Expression<Func<T, TKey>> keySelector)
        {
            return enumerable.AsQueryable().OrderBy(keySelector);
        }

        public static DocumentClientException CreateDocumentClientException(HttpStatusCode status, int retryAfter = 0)
        {
            var headers = new NameValueCollection();
            headers.Add("x-ms-retry-after-ms", retryAfter.ToString());

            var parameters = new object[] { null, null, headers, status, null };
            return Activator.CreateInstance(typeof(DocumentClientException), BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null) as DocumentClientException;
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
            [CosmosDB(Id = "abc123")] Document document,
            [CosmosDB(Id = "abc123")] Item poco,
            [CosmosDB(Id = "abc123")] object obj)
        {
        }

        private static void ClientInputParameters(
            [CosmosDB] DocumentClient client)
        {
        }
    }
}
