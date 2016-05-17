// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.ApiHub.Sdk.Table;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Table
{
    internal static class ApiHubTableAttributeExtensions
    {
        public static ITableClient GetTableClient(
            this ApiHubTableAttribute attribute,
            TableConfigContext configContext)
        {
            var connection = configContext.ConnectionFactory.CreateConnection(attribute.Connection);
            var tableClient = connection.CreateTableClient();

            return tableClient;
        }

        public static IDataSet GetDataSetReference(
            this ApiHubTableAttribute attribute,
            TableConfigContext configContext)
        {
            var tableClient = GetTableClient(attribute, configContext);
            var dataSetName =
                string.IsNullOrEmpty(attribute.DataSetName)
                    ? null
                    : configContext.NameResolver.ResolveWholeString(attribute.DataSetName);
            var dataSet = tableClient.GetDataSetReference(dataSetName);

            return dataSet;
        }

        public static ITable<TEntity> GetTableReference<TEntity>(
            this ApiHubTableAttribute attribute,
            TableConfigContext configContext)
            where TEntity : class
        {
            var dataSet = GetDataSetReference(attribute, configContext);
            var tableName = configContext.NameResolver.ResolveWholeString(attribute.TableName);
            var table = dataSet.GetTableReference<TEntity>(tableName);

            return table;
        }

        public static string GetEntityId(
            this ApiHubTableAttribute attribute,
            TableConfigContext configContext)
        {
            var entityId = configContext.NameResolver.ResolveWholeString(attribute.EntityId);

            return entityId;
        }
    }
}
