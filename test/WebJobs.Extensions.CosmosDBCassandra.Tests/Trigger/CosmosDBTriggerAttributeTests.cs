// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra.Tests.Trigger
{
    public class CosmosDBTriggerAttributeTests
    {
        [Fact]
        public void MissingArguments_Fail()
        {
            ArgumentException missingDatabaseAndCollection = Assert.Throws<ArgumentException>(() => new CosmosDBCassandraTriggerAttribute(null, null));
            Assert.Equal("tableName", missingDatabaseAndCollection.ParamName);

            ArgumentException missingDatabase = Assert.Throws<ArgumentException>(() => new CosmosDBCassandraTriggerAttribute(null, "someTable"));
            Assert.Equal("KeyspaceName", missingDatabase.ParamName);
        }

        [Fact]
        public void CompleteArguments_Succeed()
        {
            const string table = "someTable";
            const string keyspace = "someKeyspace";

            CosmosDBCassandraTriggerAttribute attribute = new CosmosDBCassandraTriggerAttribute(keyspace, table);

            Assert.Equal(table, attribute.TableName);
            Assert.Equal(keyspace, attribute.KeyspaceName);

        }
    }
}
