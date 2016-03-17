// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;

using System;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBConfigurationTests
    {
        private const string ConnectionStringKey = DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName;
        private const string EnvironmentKey = ConnectionStringKey + "_environment";
        private const string NeitherKey = ConnectionStringKey + "_neither";

        [Fact]
        public void Resolve_UsesConnectionString_First()
        {
            // Arrange
            SetEnvironment(ConnectionStringKey);

            // Act
            var connString = DocumentDBConfiguration.Resolve(ConnectionStringKey);

            // Assert            
            Assert.Equal("AccountEndpoint=https://fromconnstrings;AccountKey=some_key", connString);

            ClearEnvironment();
        }

        [Fact]
        public void Resolve_UsesEnvironment_Second()
        {
            // Arrange
            SetEnvironment(EnvironmentKey);

            // Act
            var connString = DocumentDBConfiguration.Resolve(EnvironmentKey);

            // Assert
            Assert.Equal("https://fromenvironment/", connString);

            ClearEnvironment();
        }

        [Fact]
        public void Resolve_FallsBackToNull()
        {
            // Arrange
            ClearEnvironment();

            // Act
            var connString = DocumentDBConfiguration.Resolve(NeitherKey);

            // Assert            
            Assert.Null(connString);
        }

        private static void SetEnvironment(string key)
        {
            Environment.SetEnvironmentVariable(key, "https://fromenvironment/");
        }

        private static void ClearEnvironment()
        {
            Environment.SetEnvironmentVariable(ConnectionStringKey, null);
            Environment.SetEnvironmentVariable(EnvironmentKey, null);
            Environment.SetEnvironmentVariable(NeitherKey, null);
        }
    }
}
