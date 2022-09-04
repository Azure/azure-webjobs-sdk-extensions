// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBTrigger.Tests
{
    public class CosmosDBTriggerAttributeTests
    {
        [Fact]
        public void MissingArguments_Fail()
        {
            ArgumentException missingDatabaseAndCollection = Assert.Throws<ArgumentException>(() => new CosmosDBTriggerAttribute(null, null));
            Assert.Equal("containerName", missingDatabaseAndCollection.ParamName);

            ArgumentException missingDatabase = Assert.Throws<ArgumentException>(() => new CosmosDBTriggerAttribute(null, "someCollection"));
            Assert.Equal("databaseName", missingDatabase.ParamName);
        }

        [Fact]
        public void CompleteArguments_Succeed()
        {
            const string collectionName = "someCollection";
            const string databaseName = "someDatabase";
            const string leaseCollectionName = "someLeaseCollection";
            const string leaseDatabaseName = "someLeaseDatabase";
            const string defaultLeaseCollectionName = "leases";

            CosmosDBTriggerAttribute attributeWithNoLeaseSpecified = new CosmosDBTriggerAttribute(databaseName, collectionName);

            Assert.Equal(collectionName, attributeWithNoLeaseSpecified.ContainerName);
            Assert.Equal(databaseName, attributeWithNoLeaseSpecified.DatabaseName);
            Assert.Equal(defaultLeaseCollectionName, attributeWithNoLeaseSpecified.LeaseContainerName);
            Assert.Equal(databaseName, attributeWithNoLeaseSpecified.LeaseDatabaseName);

            CosmosDBTriggerAttribute attributeWithLeaseSpecified = new CosmosDBTriggerAttribute(databaseName, collectionName) { LeaseDatabaseName = leaseDatabaseName, LeaseContainerName = leaseCollectionName };

            Assert.Equal(collectionName, attributeWithLeaseSpecified.ContainerName);
            Assert.Equal(databaseName, attributeWithLeaseSpecified.DatabaseName);
            Assert.Equal(leaseCollectionName, attributeWithLeaseSpecified.LeaseContainerName);
            Assert.Equal(leaseDatabaseName, attributeWithLeaseSpecified.LeaseDatabaseName);
        }
    }
}
