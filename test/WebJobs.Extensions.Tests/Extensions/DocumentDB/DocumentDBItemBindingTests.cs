// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBItemBindingTests
    {
        public static IEnumerable<object[]> ValidParameters
        {
            get
            {
                var itemParams = DocumentDBTestUtility.GetValidItemInputParameters().ToArray();

                var result = new[]
                {
                    new object[] { itemParams[0], typeof(DocumentDBItemValueBinder<Document>) },
                    new object[] { itemParams[1], typeof(DocumentDBItemValueBinder<Item>) },
                    new object[] { itemParams[2], typeof(DocumentDBItemValueBinder<object>) }
                };

                Assert.Equal(result.Count(), itemParams.Count());

                return result;
            }
        }

        [Theory]
        [MemberData("ValidParameters")]
        public async Task BindAsync_Returns_CorrectValueProvider(ParameterInfo parameter, Type expectedType)
        {
            // Arrange
            var easyTableContext = new DocumentDBContext();
            var bindingProviderContext = new BindingProviderContext(parameter, null, CancellationToken.None);
            var binding = new DocumentDBItemBinding(parameter, easyTableContext, bindingProviderContext);

            // Act
            var valueProvider = await binding.BindAsync("abc123", null);

            // Assert
            Assert.Equal(expectedType, valueProvider.GetType());
        }

        [Theory]
        [InlineData("{MyItemId}", "abc123")]
        [InlineData("MyItemId", "MyItemId")]
        public void ResolveId_CreatesExpectedString(string token, string expected)
        {
            // Arrange
            ParameterInfo parameter = DocumentDBTestUtility.GetValidItemInputParameters().First();

            var easyTableContext = new DocumentDBContext()
            {
                ResolvedId = token
            };

            var bindingContract = new Dictionary<string, Type>();
            bindingContract.Add("MyItemId", typeof(string));

            var bindingProviderContext = new BindingProviderContext(parameter, bindingContract, CancellationToken.None);

            var binding = new DocumentDBItemBinding(parameter, easyTableContext, bindingProviderContext);

            var bindingData = new Dictionary<string, object>();
            bindingData.Add("MyItemId", "abc123");

            // Act
            var resolved = binding.ResolveId(bindingData);

            // Assert
            Assert.Equal(expected, resolved);
        }
    }
}
