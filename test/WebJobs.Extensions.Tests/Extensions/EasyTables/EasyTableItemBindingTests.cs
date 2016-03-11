// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.EasyTables;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.EasyTables
{
    public class EasyTableItemBindingTests
    {
        public static IEnumerable<object[]> ValidParameters
        {
            get
            {
                var itemParams = EasyTableTestHelper.GetValidInputItemParameters().ToArray();

                return new[]
                {
                    new object[] { itemParams[0], typeof(EasyTableItemValueBinder<JObject>) },
                    new object[] { itemParams[1], typeof(EasyTableItemValueBinder<TodoItem>) }
                };
            }
        }

        [Theory]
        [MemberData("ValidParameters")]
        public async Task BindAsync_Returns_CorrectValueProvider(ParameterInfo parameter, Type expectedType)
        {
            // Arrange
            var easyTableContext = new EasyTableContext();
            var bindingProviderContext = new BindingProviderContext(parameter, null, CancellationToken.None);
            var binding = new EasyTableItemBinding(parameter, easyTableContext, bindingProviderContext);

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
            ParameterInfo parameter = EasyTableTestHelper.GetValidInputItemParameters().First();

            var easyTableContext = new EasyTableContext()
            {
                ResolvedId = token
            };

            var bindingContract = new Dictionary<string, Type>();
            bindingContract.Add("MyItemId", typeof(string));

            var bindingProviderContext = new BindingProviderContext(parameter, bindingContract, CancellationToken.None);

            var binding = new EasyTableItemBinding(parameter, easyTableContext, bindingProviderContext);

            var bindingData = new Dictionary<string, object>();
            bindingData.Add("MyItemId", "abc123");

            // Act
            var resolved = binding.ResolveId(bindingData);

            // Assert
            Assert.Equal(expected, resolved);
        }
    }
}