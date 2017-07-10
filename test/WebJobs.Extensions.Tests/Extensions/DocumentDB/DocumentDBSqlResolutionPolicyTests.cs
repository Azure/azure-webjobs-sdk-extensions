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

        [Fact]
        public void TemplateBind_StringListParameters()
        {
            // Arrange
            PropertyInfo propInfo = null;
            DocumentDBAttribute resolvedAttribute = new DocumentDBAttribute();
            BindingTemplate bindingTemplate =
                BindingTemplate.FromString("SELECT * FROM c WHERE c.id = {foo.name}");
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("foo", new Dictionary<string, string>() { { "name", "bar" } });
            DocumentDBSqlResolutionPolicy policy = new DocumentDBSqlResolutionPolicy();

            // Act
            string result = policy.TemplateBind(propInfo, resolvedAttribute, bindingTemplate, bindingData);

            // Assert
            Assert.Single(resolvedAttribute.SqlQueryParameters, p => p.Name == "@fooname" && p.Value.ToString() == "bar");
            Assert.Equal("SELECT * FROM c WHERE c.id = @fooname", result);
        }

        [Fact]
        public void TemplateBind_MultipleStringListParameters()
        {
            // Arrange
            PropertyInfo propInfo = null;
            DocumentDBAttribute resolvedAttribute = new DocumentDBAttribute();
            BindingTemplate bindingTemplate =
                BindingTemplate.FromString("SELECT * FROM c WHERE c.id = {foo.name} AND c.val1 = {foo.age} AND c.val2 = {fizz.buzz}");
            Dictionary<string, object> bindingData = new Dictionary<string, object>();
            bindingData.Add("foo", new Dictionary<string, string>() { { "name", "bar" }, { "age", "13" } });
            bindingData.Add("fizz", new Dictionary<string, string>() { { "buzz", "hello" } });
            DocumentDBSqlResolutionPolicy policy = new DocumentDBSqlResolutionPolicy();

            // Act
            string result = policy.TemplateBind(propInfo, resolvedAttribute, bindingTemplate, bindingData);

            // Assert
            Assert.Single(resolvedAttribute.SqlQueryParameters, p => p.Name == "@fooname" && p.Value.ToString() == "bar");
            Assert.Single(resolvedAttribute.SqlQueryParameters, p => p.Name == "@fooage" && p.Value.ToString() == "13");
            Assert.Single(resolvedAttribute.SqlQueryParameters, p => p.Name == "@fizzbuzz" && p.Value.ToString() == "hello");
            Assert.Equal("SELECT * FROM c WHERE c.id = @fooname AND c.val1 = @fooage AND c.val2 = @fizzbuzz", result);
        }
    }
}
