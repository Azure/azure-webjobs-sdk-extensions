// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Reflection;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBSqlResolutionPolicyTests
    {
        [Fact]
        public void TemplateBind_MultipleParameters()
        {
            // Arrange
            PropertyInfo propInfo = null;
            DocumentDBAttribute resolvedAttribute = new DocumentDBAttribute();
            BindingTemplate bindingTemplate = 
                BindingTemplate.FromString("SELECT * FROM c WHERE c.id = {foo} AND c.value = {bar}");
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("foo", "1234");
            bindingData.Add("bar", "5678");
            DocumentDBSqlResolutionPolicy policy = new DocumentDBSqlResolutionPolicy();

            // Act
            string result = policy.TemplateBind(propInfo, resolvedAttribute, bindingTemplate, bindingData);

            // Assert
            Assert.Single(resolvedAttribute.SqlQueryParameters, p => p.Name == "@foo" && p.Value.ToString() == "1234");
            Assert.Single(resolvedAttribute.SqlQueryParameters, p => p.Name == "@bar" && p.Value.ToString() == "5678");
            Assert.Equal("SELECT * FROM c WHERE c.id = @foo AND c.value = @bar", result);
        }

        [Fact]
        public void TemplateBind_DuplicateParameters()
        {
            // Arrange
            PropertyInfo propInfo = null;
            DocumentDBAttribute resolvedAttribute = new DocumentDBAttribute();
            BindingTemplate bindingTemplate =
                BindingTemplate.FromString("SELECT * FROM c WHERE c.id = {foo} AND c.value = {foo}");
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("foo", "1234");
            DocumentDBSqlResolutionPolicy policy = new DocumentDBSqlResolutionPolicy();

            // Act
            string result = policy.TemplateBind(propInfo, resolvedAttribute, bindingTemplate, bindingData);

            // Assert
            Assert.Single(resolvedAttribute.SqlQueryParameters, p => p.Name == "@foo" && p.Value.ToString() == "1234");
            Assert.Equal("SELECT * FROM c WHERE c.id = @foo AND c.value = @foo", result);
        }
    }
}
