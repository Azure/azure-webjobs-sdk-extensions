// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Trigger
{
    public class CosmosDBTriggerAttributeBindingProviderTests
    {
        [Theory]
        [MemberData("InvalidCosmosDBTriggerParameters")]
        public async Task InvalidParameters_Fail(ParameterInfo parameter)
        {
            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider("notAConnectionString", string.Empty);
            
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None)));

            Assert.NotNull(ex);            
        }

        [Theory]
        [MemberData("ValidCosmosDBTriggerParameters")]
        public async Task ValidParameters_Succeed(ParameterInfo parameter)
        {
            CosmosDBTriggerAttributeBindingProvider provider = new CosmosDBTriggerAttributeBindingProvider("AccountEndpoint=https://someEndpoint;AccountKey=someKey;", string.Empty);

            ITriggerBinding binding = await provider.TryCreateAsync(new TriggerBindingProviderContext(parameter, CancellationToken.None));

            Assert.Equal(binding.TriggerValueType, typeof(IReadOnlyList<Document>));
        }

        public static IEnumerable<ParameterInfo[]> InvalidCosmosDBTriggerParameters
        {
            get { return InvalidCosmosDBTriggerBindigs.GetParameters(); }
        }

        public static IEnumerable<ParameterInfo[]> ValidCosmosDBTriggerParameters
        {
            get { return ValidCosmosDBTriggerBindigs.GetParameters(); }
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

            public static void Func3([CosmosDBTrigger("aDatabase", "aCollection")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func4([CosmosDBTrigger("aDatabase", "aCollection", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aCollection")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func5([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "notAConnectionString")] object docs)
            {

            }

            public static void Func6([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "AccountEndpoint=https://someEndpoint;AccountKey=someKey;", LeaseConnectionStringSetting = "AccountEndpoint=https://someEndpoint;AccountKey=someKey;", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aCollection")] IReadOnlyList<Document> docs)
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
                    new[] { GetFirstParameter(type, "Func4") },
                    new[] { GetFirstParameter(type, "Func5") },
                    new[] { GetFirstParameter(type, "Func6") }
                };
            }
        }

        private static class ValidCosmosDBTriggerBindigs
        {
            public static void Func1([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "AccountEndpoint=https://someEndpoint;AccountKey=someKey;")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func2([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "AccountEndpoint=https://someEndpoint;AccountKey=someKey;", LeaseConnectionStringSetting = "AccountEndpoint=https://someEndpoint;AccountKey=someKey;", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aLeaseCollection")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func3([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "AccountEndpoint=https://someEndpoint;AccountKey=someKey;", LeaseDatabaseName = "aDatabase", LeaseCollectionName = "aLeaseCollection")] IReadOnlyList<Document> docs)
            {

            }

            public static void Func4([CosmosDBTrigger("aDatabase", "aCollection", ConnectionStringSetting = "AccountEndpoint=https://someEndpoint;AccountKey=someKey;")] JArray docs)
            {

            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(ValidCosmosDBTriggerBindigs);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") },
                    new[] { GetFirstParameter(type, "Func4") }
                };
            }
        }
    }
}
