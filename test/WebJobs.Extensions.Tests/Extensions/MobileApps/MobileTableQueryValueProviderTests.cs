// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.WindowsAzure.MobileServices;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps
{
    public class MobileTableQueryValueProviderTests
    {
        [Fact]
        public void GetValue_ReturnsCorrectType()
        {
            var parameter = MobileAppTestHelper.GetValidInputQueryParameters().Single();
            var context = new MobileTableContext()
            {
                Client = new MobileServiceClient("http://someuri")
            };
            var provider = new MobileTableQueryValueProvider<TodoItem>(parameter, context);

            var value = provider.GetValue();

            Assert.True(typeof(IMobileServiceTableQuery<TodoItem>).IsAssignableFrom(value.GetType()));
        }

        [Fact]
        public void GetValue_WithTableName_ReturnsCorrectType()
        {
            var parameter = MobileAppTestHelper.GetValidInputQueryParameters().Single();
            var context = new MobileTableContext()
            {
                Client = new MobileServiceClient("http://someuri"),
                ResolvedTableName = "SomeOtherTable"
            };
            var provider = new MobileTableQueryValueProvider<TodoItem>(parameter, context);

            var value = provider.GetValue() as IMobileServiceTableQuery<TodoItem>;

            Assert.NotNull(value);
            Assert.Equal("SomeOtherTable", value.Table.TableName);
        }
    }
}