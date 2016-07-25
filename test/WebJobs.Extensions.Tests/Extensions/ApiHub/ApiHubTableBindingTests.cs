// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Table;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.ApiHub
{
    [Trait("Category", "E2E")]
    public class ApiHubTableBindingTests
    {
        public static IEnumerable<ParameterInfo[]> InvalidTableClientParameters
        {
            get { return InvalidTableClientBindings.GetParameters(); }
        }

        public static IEnumerable<ParameterInfo[]> InvalidTableParameters
        {
            get { return InvalidTableBindings.GetParameters(); }
        }

        public static IEnumerable<ParameterInfo[]> InvalidEntityParameters
        {
            get { return InvalidEntityBindings.GetParameters(); }
        }

        [Fact]
        public async Task BindToTableClientAndAddEntity()
        {
            var adapter = new FakeTabularConnectorAdapter();
            adapter.AddDataSet("default");
            adapter.AddTable("default", "table1", "Id");

            using (var host = CreateTestJobHost(adapter))
            {
                await host.StartAsync();
                await host.CallAsync(typeof(ApiHubTableBindingTests).GetMethod("BindToTableClientAndAddEntityFunc"));
                await host.StopAsync();
            }

            var entity = await adapter.GetEntityAsync<SampleEntity>("default", "table1", "1");
            Assert.NotNull(entity);
            Assert.Equal(1, entity.Id);
            Assert.Equal("foo", entity.Text);
        }

        public static async void BindToTableClientAndAddEntityFunc(
            [ApiHubTable("AzureWebJobsSql")]
            ITableClient tableClient)
        {
            var defaultDataSet = tableClient.GetDataSetReference();
            var table = defaultDataSet.GetTableReference<SampleEntity>("table1");

            await table.CreateEntityAsync(
                new SampleEntity
                {
                    Id = 1,
                    Text = "foo"
                });
        }

        [Fact]
        public async Task BindToTableAndAddEntity()
        {
            var adapter = new FakeTabularConnectorAdapter();
            adapter.AddDataSet("dataset1");
            adapter.AddTable("dataset1", "table1", "Id");

            TestInput input = new TestInput
            {
                DataSet = "dataset1",
                Table = "table1"
            };
            var args = new Dictionary<string, object>()
                {
                    { "input", JsonConvert.SerializeObject(input) }
                };

            using (var host = CreateTestJobHost(adapter))
            {
                await host.StartAsync();
                await host.CallAsync(typeof(ApiHubTableBindingTests).GetMethod("BindToTableAndAddEntityFunc"), args);
                await host.StopAsync();
            }

            var entity = await adapter.GetEntityAsync<SampleEntity>("dataset1", "table1", "1");
            Assert.NotNull(entity);
            Assert.Equal(1, entity.Id);
            Assert.Equal("foo", entity.Text);
        }

        public static async void BindToTableAndAddEntityFunc(
            [QueueTrigger("testqueue")] TestInput input,
            [ApiHubTable("AzureWebJobsSql", DataSetName = "{DataSet}", TableName = "{Table}")]
            ITable<SampleEntity> table)
        {
            await table.CreateEntityAsync(
                new SampleEntity
                {
                    Id = 1,
                    Text = "foo"
                });
        }

        [Fact]
        public async Task BindToTableOfJObjectAndAddEntity()
        {
            var adapter = new FakeTabularConnectorAdapter();
            adapter.AddDataSet("default");
            adapter.AddTable("default", "table1", "Id");

            using (var host = CreateTestJobHost(adapter))
            {
                await host.StartAsync();
                await host.CallAsync(typeof(ApiHubTableBindingTests).GetMethod("BindToTableOfJObjectAndAddEntityFunc"));
                await host.StopAsync();
            }

            var entity = await adapter.GetEntityAsync<JObject>("default", "table1", "1");
            Assert.NotNull(entity);
            Assert.Equal(1, entity["Id"].Value<int>());
            Assert.Equal("foo", entity["Text"].Value<string>());
        }

        public static async void BindToTableOfJObjectAndAddEntityFunc(
            [ApiHubTable("AzureWebJobsSql", TableName = "table1")]
            ITable<JObject> table)
        {
            await table.CreateEntityAsync(
                new JObject(
                    new JProperty("Id", 1),
                    new JProperty("Text", "foo")));
        }

        [Fact]
        public async Task BindToAsyncCollectorAndAddEntity()
        {
            var adapter = new FakeTabularConnectorAdapter();
            adapter.AddDataSet("default");
            adapter.AddTable("default", "table1", "Id");

            using (var host = CreateTestJobHost(adapter))
            {
                await host.StartAsync();
                await host.CallAsync(typeof(ApiHubTableBindingTests).GetMethod("BindToAsyncCollectorAndAddEntityFunc"));
                await host.StopAsync();
            }

            var entity = await adapter.GetEntityAsync<SampleEntity>("default", "table1", "1");
            Assert.NotNull(entity);
            Assert.Equal(1, entity.Id);
            Assert.Equal("foo", entity.Text);
        }

        public static async void BindToAsyncCollectorAndAddEntityFunc(
            [ApiHubTable("AzureWebJobsSql", TableName = "table1")]
            IAsyncCollector<SampleEntity> collector)
        {
            await collector.AddAsync(
                new SampleEntity
                {
                    Id = 1,
                    Text = "foo"
                });
        }

        [Fact]
        public async void BindToAsyncCollectorOfJObjectAndAddEntity()
        {
            var adapter = new FakeTabularConnectorAdapter();
            adapter.AddDataSet("default");
            adapter.AddTable("default", "table1", "Id");

            using (var host = CreateTestJobHost(adapter))
            {
                await host.StartAsync();
                await host.CallAsync(typeof(ApiHubTableBindingTests).GetMethod("BindToAsyncCollectorOfJObjectAndAddEntityFunc"));
                await host.StopAsync();
            }

            var entity = await adapter.GetEntityAsync<JObject>("default", "table1", "1");
            Assert.NotNull(entity);
            Assert.Equal(1, entity["Id"].Value<int>());
            Assert.Equal("foo", entity["Text"].Value<string>());
        }

        public static async void BindToAsyncCollectorOfJObjectAndAddEntityFunc(
            [ApiHubTable("AzureWebJobsSql", TableName = "table1")]
            IAsyncCollector<JObject> collector)
        {
            await collector.AddAsync(
                new JObject(
                    new JProperty("Id", 1),
                    new JProperty("Text", "foo")));
        }

        [Fact]
        public async void BindToEntityAndUpdate()
        {
            var adapter = new FakeTabularConnectorAdapterWithUpdateCount();
            adapter.AddDataSet("default");
            adapter.AddTable("default", "table1", "Id");
            await adapter.CreateEntityAsync(
                "default",
                "table1",
                new SampleEntity
                {
                    Id = 1,
                    Text = "foo"
                });

            using (var host = CreateTestJobHost(adapter))
            {
                await host.StartAsync();
                await host.CallAsync(typeof(ApiHubTableBindingTests).GetMethod("BindToEntityAndUpdateFunc"));
                await host.StopAsync();
            }

            var entity = await adapter.GetEntityAsync<SampleEntity>("default", "table1", "1");
            Assert.NotNull(entity);
            Assert.Equal(1, entity.Id);
            Assert.Equal("bar", entity.Text);
            Assert.Equal(1, adapter.UpdateCount);
        }

        public static void BindToEntityAndUpdateFunc(
            [ApiHubTable("AzureWebJobsSql", TableName = "table1", EntityId = "1")]
            SampleEntity entity)
        {
            entity.Text = "bar";
        }

        [Fact]
        public async void BindToEntityWithBindingParameters()
        {
            var adapter = new FakeTabularConnectorAdapterWithUpdateCount();
            adapter.AddDataSet("default");
            adapter.AddTable("default", "table1", "Id");
            await adapter.CreateEntityAsync(
                "default",
                "table1",
                new SampleEntity
                {
                    Id = 1,
                    Text = "foo"
                });

            TestInput input = new TestInput
            {
                Id = 1,
                Table = "table1"
            };
            var args = new Dictionary<string, object>()
                {
                    { "input", JsonConvert.SerializeObject(input) }
                };

            using (var host = CreateTestJobHost(adapter))
            {
                await host.StartAsync();
                await host.CallAsync(typeof(ApiHubTableBindingTests).GetMethod("BindToEntityWithBindingParametersFunc"), args);
                await host.StopAsync();
            }

            var entity = await adapter.GetEntityAsync<SampleEntity>("default", "table1", "1");
            Assert.NotNull(entity);
            Assert.Equal(1, entity.Id);
            Assert.Equal("bar", entity.Text);
            Assert.Equal(1, adapter.UpdateCount);
        }

        public static void BindToEntityWithBindingParametersFunc(
            [QueueTrigger("testqueue")] TestInput input,
            [ApiHubTable("AzureWebJobsSql", TableName = "{Table}", EntityId = "{Id}")] SampleEntity entity)
        {
            entity.Text = "bar";
        }

        [Fact]
        public async void BindToJObjectAndUpdate()
        {
            var adapter = new FakeTabularConnectorAdapterWithUpdateCount();
            adapter.AddDataSet("default");
            adapter.AddTable("default", "table1", "Id");
            await adapter.CreateEntityAsync(
                "default",
                "table1",
                new JObject(
                    new JProperty("Id", 1),
                    new JProperty("Text", "foo")));

            using (var host = CreateTestJobHost(adapter))
            {
                await host.StartAsync();
                await host.CallAsync(typeof(ApiHubTableBindingTests).GetMethod("BindToJObjectAndUpdateFunc"));
                await host.StopAsync();
            }

            var entity = await adapter.GetEntityAsync<JObject>("default", "table1", "1");
            Assert.NotNull(entity);
            Assert.Equal(1, entity["Id"].Value<int>());
            Assert.Equal("bar", entity["Text"].Value<string>());
            Assert.Equal(1, adapter.UpdateCount);
        }

        public static void BindToJObjectAndUpdateFunc(
            [ApiHubTable("AzureWebJobsSql", TableName = "table1", EntityId = "1")]
            JObject entity)
        {
            entity["Text"] = "bar";
        }

        [Fact]
        public async void BindToEntityNoUpdate()
        {
            var adapter = new FakeTabularConnectorAdapterWithUpdateCount();
            adapter.AddDataSet("default");
            adapter.AddTable("default", "table1", "Id");
            await adapter.CreateEntityAsync(
                "default",
                "table1",
                new SampleEntity
                {
                    Id = 1,
                    Text = "foo"
                });

            using (var host = CreateTestJobHost(adapter))
            {
                await host.StartAsync();
                await host.CallAsync(typeof(ApiHubTableBindingTests).GetMethod("BindToEntityNoUpdateFunc"));
                await host.StopAsync();
            }

            var entity = await adapter.GetEntityAsync<SampleEntity>("default", "table1", "1");
            Assert.NotNull(entity);
            Assert.Equal(1, entity.Id);
            Assert.Equal("foo", entity.Text);
            Assert.Equal(0, adapter.UpdateCount);
        }

        public static void BindToEntityNoUpdateFunc(
            [ApiHubTable("AzureWebJobsSql", TableName = "table1", EntityId = "1")]
            JObject entity)
        {
        }

        [Theory]
        [MemberData("InvalidTableClientParameters")]
        public async void ProviderThrowsIfInvalidTableClientBinding(ParameterInfo parameter)
        {
            var provider = new TableBindingProvider(null);
            var context = new BindingProviderContext(parameter, null, CancellationToken.None);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.TryCreateAsync(context));

            Assert.True(exception.Message.Contains(typeof(ApiHubTableAttribute).Name));
        }

        [Theory]
        [MemberData("InvalidTableParameters")]
        public async void ProviderThrowsIfInvalidTableBinding(ParameterInfo parameter)
        {
            var provider = new TableBindingProvider(null);
            var context = new BindingProviderContext(parameter, null, CancellationToken.None);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.TryCreateAsync(context));

            Assert.True(exception.Message.Contains(typeof(ApiHubTableAttribute).Name));
        }

        [Theory]
        [MemberData("InvalidEntityParameters")]
        public async void ProviderThrowsIfInvalidEntityBinding(ParameterInfo parameter)
        {
            var provider = new TableBindingProvider(null);
            var context = new BindingProviderContext(parameter, null, CancellationToken.None);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.TryCreateAsync(context));

            Assert.True(exception.Message.Contains(typeof(ApiHubTableAttribute).Name));
        }

        private static JobHost CreateTestJobHost(FakeTabularConnectorAdapter adapter)
        {
            var configuration = new JobHostConfiguration
            {
                TypeLocator = new ExplicitTypeLocator(typeof(ApiHubTableBindingTests))
            };

            var connectionFactory = new FakeConnectionFactory(adapter);

            configuration.UseApiHub(new ApiHubConfiguration(connectionFactory));

            return new JobHost(configuration);
        }

        private static ParameterInfo GetFirstParameter(Type type, string methodName)
        {
            var methodInfo = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            var paramInfo = methodInfo.GetParameters().First();

            return paramInfo;
        }

        public class SampleEntity
        {
            public int Id { get; set; }
            public string Text { get; set; }
        }

        private class FakeTabularConnectorAdapterWithUpdateCount : FakeTabularConnectorAdapter
        {
            public int UpdateCount { get; set; }

            public override Task UpdateEntityAsync<TEntity>(
                string dataSetName,
                string tableName,
                string entityId,
                TEntity entity,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                UpdateCount++;

                return base.UpdateEntityAsync<TEntity>(
                    dataSetName,
                    tableName,
                    entityId,
                    entity,
                    cancellationToken);
            }
        }

        private static class InvalidTableClientBindings
        {
            public static void Func1(
                [ApiHubTable("AzureWebJobsSql")]
                out ITableClient tableClient)
            {
                tableClient = null;
            }

            public static void Func2(
                [ApiHubTable("AzureWebJobsSql", TableName = "table1")]
                ITableClient tableClient)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(InvalidTableClientBindings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") }
                };
            }
        }

        private static class InvalidTableBindings
        {
            public static void Func1(
                [ApiHubTable("AzureWebJobsSql", TableName = "table1")]
                out ITable<SampleEntity> table)
            {
                table = null;
            }

            public static void Func2(
                [ApiHubTable("AzureWebJobsSql")]
                ITable<SampleEntity> table)
            {
            }

            public static void Func3(
                [ApiHubTable("AzureWebJobsSql", TableName = "table1", EntityId = "1")]
                ITable<SampleEntity> table)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(InvalidTableBindings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") }
                };
            }
        }

        private static class InvalidEntityBindings
        {
            public static void Func1(
                [ApiHubTable("AzureWebJobsSql", TableName = "table1", EntityId = "1")]
                out SampleEntity entity)
            {
                entity = null;
            }

            public static void Func2(
                [ApiHubTable("AzureWebJobsSql")]
                SampleEntity entity)
            {
            }

            public static void Func3(
                [ApiHubTable("AzureWebJobsSql", TableName = "table1")]
                SampleEntity entity)
            {
            }

            public static void Func4(
                [ApiHubTable("AzureWebJobsSql", EntityId = "1")]
                SampleEntity entity)
            {
            }

            public static IEnumerable<ParameterInfo[]> GetParameters()
            {
                var type = typeof(InvalidEntityBindings);

                return new[]
                {
                    new[] { GetFirstParameter(type, "Func1") },
                    new[] { GetFirstParameter(type, "Func2") },
                    new[] { GetFirstParameter(type, "Func3") },
                    new[] { GetFirstParameter(type, "Func4") }
                };
            }
        }

        public class TestInput
        {
            public int Id { get; set; }
            public string Table { get; set; }
            public string DataSet { get; set; }
        }
    }
}
