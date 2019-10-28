// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBSqlResolutionPolicyTests
    {
        [Fact]
        public void TemplateBind_MultipleParameters()
        {
            // Arrange
            string query = "SELECT * FROM c WHERE c.id = {foo} AND c.value = {bar}";
            PropertyInfo propInfo = null;
            CosmosDBAttribute resolvedAttribute = new CosmosDBAttribute() { SqlQuery = query };
            BindingTemplate bindingTemplate =
                BindingTemplate.FromString(query);
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("foo", "1234");
            bindingData.Add("bar", "5678");
            CosmosDBSqlResolutionPolicy policy = new CosmosDBSqlResolutionPolicy();

            // Act
            string result = policy.TemplateBind(propInfo, resolvedAttribute, bindingTemplate, bindingData);

            // Assert
            Assert.Single(resolvedAttribute.SqlQueryParameters, p => p.Item1 == "@foo" && p.Item2.ToString() == "1234");
            Assert.Single(resolvedAttribute.SqlQueryParameters, p => p.Item1 == "@bar" && p.Item2.ToString() == "5678");

            Assert.Equal("SELECT * FROM c WHERE c.id = @foo AND c.value = @bar", result);
        }

        [Fact]
        public void TemplateBind_DuplicateParameters()
        {
            // Arrange
            string query = "SELECT * FROM c WHERE c.id = {foo} AND c.value = {foo}";
            PropertyInfo propInfo = null;
            CosmosDBAttribute resolvedAttribute = new CosmosDBAttribute() { SqlQuery = query };
            BindingTemplate bindingTemplate =
                BindingTemplate.FromString(query);
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("foo", "1234");
            CosmosDBSqlResolutionPolicy policy = new CosmosDBSqlResolutionPolicy();

            // Act
            string result = policy.TemplateBind(propInfo, resolvedAttribute, bindingTemplate, bindingData);

            // Assert
            Assert.Single(resolvedAttribute.SqlQueryParameters, p => p.Item1 == "@foo" && p.Item2.ToString() == "1234");
            Assert.Equal("SELECT * FROM c WHERE c.id = @foo AND c.value = @foo", result);
        }
    }
}
