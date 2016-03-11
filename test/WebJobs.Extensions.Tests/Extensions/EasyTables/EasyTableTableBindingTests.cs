// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.EasyTables;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.EasyTables
{
    public class EasyTableTableBindingTests
    {
        public static IEnumerable<object[]> ValidParameters
        {
            get
            {
                var itemParams = EasyTableTestHelper.GetValidInputTableParameters().ToArray();

                return new[]
                {
                    new object[] { itemParams[0], typeof(EasyTableTableValueProvider<JObject>) },
                    new object[] { itemParams[1], typeof(EasyTableTableValueProvider<TodoItem>) }
                };
            }
        }

        [Theory]
        [MemberData("ValidParameters")]
        public async Task BindAsync_Returns_CorrectValueProvider(ParameterInfo parameter, Type expectedType)
        {
            // Arrange
            var easyTableContext = new EasyTableContext();
            var binding = new EasyTableTableBinding(parameter, easyTableContext);

            // Act
            var valueProvider = await binding.BindAsync(null, null);

            // Assert
            Assert.Equal(expectedType, valueProvider.GetType());
        }

        [Theory]
        [InlineData(typeof(IMobileServiceTable), true)]
        [InlineData(typeof(IMobileServiceTable<TodoItem>), true)]
        [InlineData(typeof(IMobileServiceTable<NoId>), false)]
        [InlineData(typeof(TodoItem), false)]
        public void IsMobileServiceTableType_CorrectlyValidates(Type tableType, bool expected)
        {
            // Act
            bool result = EasyTableTableBinding.IsMobileServiceTableType(tableType);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}