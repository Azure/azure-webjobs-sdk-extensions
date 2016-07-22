// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace ExtensionsSample.Samples
{
    // To use the ApiHubTableSamples:
    // 1. Add an AzureWebJobsSql app setting or environnment variable for your ApiHub SQL connection. 
    //    The format should be: Endpoint={endpoint};Scheme={scheme};AccessToken={accesstoken}
    // 2. Call config.UseApiHub() in Program.cs
    // 3. Add typeof(ApiHubTableSamples) to the SamplesTypeLocator in Program.cs
    // 4. Create table1 in your SQL database:
    //      CREATE TABLE SampleTable
    //      (
    //          Id int NOT NULL,
    //          Text nvarchar(10) NULL
    //          CONSTRAINT PK_Id PRIMARY KEY(Id)
    //      )
    public static class ApiHubTableSamples
    {
        public static void BindToTableClient(
            [ApiHubTable("AzureWebJobsSql")]
            ITableClient tableClient)
        {
            // Use the table client.
        }

        public static void BindToTableOfJObject(
            [ApiHubTable("AzureWebJobsSql", TableName = "SampleTable")]
            ITable<JObject> table)
        {
            // Use the table.
        }

        public static void BindToTableOfSampleEntity(
            [ApiHubTable("AzureWebJobsSql", TableName = "SampleTable")]
            ITable<SampleEntity> table)
        {
            // Use the table.
        }

        public static void BindToAsyncCollectorOfJObject(
            [ApiHubTable("AzureWebJobsSql", TableName = "SampleTable")]
            IAsyncCollector<JObject> collector)
        {
            // Add entities to the collector.
        }

        public static void BindToAsyncCollectorOfSampleEntity(
            [ApiHubTable("AzureWebJobsSql", TableName = "SampleTable")]
            IAsyncCollector<SampleEntity> collector)
        {
            // Add entities to the collector.
        }

        public static void BindToJObject(
            [ApiHubTable("AzureWebJobsSql", TableName = "SampleTable", EntityId = "1")]
            JObject entity)
        {
            // Use or update the entity.
        }

        public static void BindToSampleEntity(
            [ApiHubTable("AzureWebJobsSql", TableName = "SampleTable", EntityId = "1")]
            SampleEntity entity)
        {
            // Use or update the entity.
        }

        public class SampleEntity
        {
            public int Id { get; set; }

            public string Text { get; set; }
        }
    }
}
