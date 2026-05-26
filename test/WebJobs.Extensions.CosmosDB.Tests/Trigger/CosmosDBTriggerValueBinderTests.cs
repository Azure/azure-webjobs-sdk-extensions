// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBTrigger.Tests
{
    /// <summary>
    /// Targets the AVAD type-shim behavior of <see cref="CosmosDBTriggerValueBinder"/>.
    /// </summary>
    public class CosmosDBTriggerValueBinderTests
    {
        [Fact]
        public void Type_JArrayParameter_LatestVersion_ReturnsReadOnlyCollectionOfJObject()
        {
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.JArrayLatestVersion));
            var attribute = new CosmosDBTriggerAttribute("db", "coll");

            var binder = new CosmosDBTriggerValueBinder(parameter, value: new JArray(), cosmosDBAttribute: attribute);

            Assert.Equal(typeof(IReadOnlyCollection<JObject>), binder.Type);
        }

        [Fact]
        public void Type_StringParameter_LatestVersion_ReturnsReadOnlyCollectionOfJObject()
        {
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.StringLatestVersion));
            var attribute = new CosmosDBTriggerAttribute("db", "coll");

            var binder = new CosmosDBTriggerValueBinder(parameter, value: "[]", cosmosDBAttribute: attribute);

            Assert.Equal(typeof(IReadOnlyCollection<JObject>), binder.Type);
        }

        [Fact]
        public void Type_JArrayParameter_AllVersionsAndDeletes_ReturnsReadOnlyCollectionOfChangeFeedItem()
        {
            // The AVAD shim: when the function uses an untyped JArray binding but the trigger is
            // in AllVersionsAndDeletes mode, the host's change feed processor delivers
            // IReadOnlyCollection<ChangeFeedItem<JObject>>; the binder must surface that wrapped
            // type so the runtime can flow the value through to the worker without a cast failure.
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.JArrayAllVersionsAndDeletes));
            var attribute = new CosmosDBTriggerAttribute("db", "coll")
            {
                ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes
            };

            var binder = new CosmosDBTriggerValueBinder(parameter, value: new JArray(), cosmosDBAttribute: attribute);

            Assert.Equal(typeof(IReadOnlyCollection<ChangeFeedItem<JObject>>), binder.Type);
        }

        [Fact]
        public void Type_StringParameter_AllVersionsAndDeletes_ReturnsReadOnlyCollectionOfChangeFeedItem()
        {
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.StringAllVersionsAndDeletes));
            var attribute = new CosmosDBTriggerAttribute("db", "coll")
            {
                ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes
            };

            var binder = new CosmosDBTriggerValueBinder(parameter, value: "[]", cosmosDBAttribute: attribute);

            Assert.Equal(typeof(IReadOnlyCollection<ChangeFeedItem<JObject>>), binder.Type);
        }

        [Fact]
        public void Type_TypedParameter_LatestVersion_ReturnsParameterType()
        {
            // Strongly-typed parameters bypass the JArray/string shim entirely; Type
            // should reflect the user's declared parameter type as-is.
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.TypedLatestVersion));
            var attribute = new CosmosDBTriggerAttribute("db", "coll");

            var binder = new CosmosDBTriggerValueBinder(parameter, value: new List<MyDocument>(), cosmosDBAttribute: attribute);

            Assert.Equal(typeof(IReadOnlyList<MyDocument>), binder.Type);
        }

        [Fact]
        public void Type_TypedParameter_AllVersionsAndDeletes_ReturnsParameterType()
        {
            // When the user already binds to IReadOnlyList<ChangeFeedItem<T>>, the AVAD shim
            // should not interfere - the parameter type is already correct and the binder must
            // not double-wrap into IReadOnlyCollection<ChangeFeedItem<...>>.
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.TypedAllVersionsAndDeletes));
            var attribute = new CosmosDBTriggerAttribute("db", "coll")
            {
                ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes
            };

            var binder = new CosmosDBTriggerValueBinder(parameter, value: new List<ChangeFeedItem<MyDocument>>(), cosmosDBAttribute: attribute);

            Assert.Equal(typeof(IReadOnlyList<ChangeFeedItem<MyDocument>>), binder.Type);
        }

        [Fact]
        public void Type_NullAttribute_Defaults_To_LatestVersion()
        {
            // The cosmosDBAttribute parameter is optional for backwards compatibility; a null
            // attribute must not crash the Type getter and must behave as LatestVersion.
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.JArrayLatestVersion));

            var binder = new CosmosDBTriggerValueBinder(parameter, value: new JArray());

            Assert.Equal(typeof(IReadOnlyCollection<JObject>), binder.Type);
        }

        [Fact]
        public async Task GetValueAsync_AllVersionsAndDeletes_ReturnsJArrayValueUnchanged()
        {
            // GetValueAsync is unaffected by the AVAD shim - it still produces a JArray for
            // JArray-bound parameters. Only the declared Type changes.
            ParameterInfo parameter = GetFirstParameter(nameof(SampleFunctions.JArrayAllVersionsAndDeletes));
            var attribute = new CosmosDBTriggerAttribute("db", "coll")
            {
                ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes
            };
            var payload = new JArray(new JObject { ["id"] = "1" });

            var binder = new CosmosDBTriggerValueBinder(parameter, value: payload, cosmosDBAttribute: attribute);
            object value = await binder.GetValueAsync();

            var jArray = Assert.IsType<JArray>(value);
            Assert.Single(jArray);
            Assert.Equal("1", (string)jArray[0]["id"]);
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
            public static void JArrayLatestVersion([CosmosDBTrigger("db", "coll")] JArray docs)
            {
            }

            public static void JArrayAllVersionsAndDeletes(
                [CosmosDBTrigger("db", "coll", ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes)] JArray docs)
            {
            }

            public static void StringLatestVersion([CosmosDBTrigger("db", "coll")] string docs)
            {
            }

            public static void StringAllVersionsAndDeletes(
                [CosmosDBTrigger("db", "coll", ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes)] string docs)
            {
            }

            public static void TypedLatestVersion([CosmosDBTrigger("db", "coll")] IReadOnlyList<MyDocument> docs)
            {
            }

            public static void TypedAllVersionsAndDeletes(
                [CosmosDBTrigger("db", "coll", ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes)] IReadOnlyList<ChangeFeedItem<MyDocument>> docs)
            {
            }
        }
    }
}
