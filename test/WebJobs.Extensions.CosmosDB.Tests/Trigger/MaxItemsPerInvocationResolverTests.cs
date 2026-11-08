// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class MaxItemsPerInvocationResolverTests
    {
        [Theory]
        [InlineData(25, null, "30", null, 25)]
        [InlineData(0, "limit", "30", "20", 20)]
        [InlineData(-1, "limit", "30", "20", 20)]
        [InlineData(0, null, "30", null, 30)]
        [InlineData(-2, null, null, null, -2)]
        [InlineData(0, null, null, null, 0)]
        [InlineData(2147483647, null, null, null, 2147483647)]
        [InlineData(0, "limit", null, "2147483647", 2147483647)]
        public void ResolvesPrecedenceAndLegacyValues(int explicitLimit, string settingName, string appDefaultValue, string resolvedSettingValue, int expectedLimit)
        {
            int result = MaxItemsPerInvocationResolver.Resolve(explicitLimit, settingName, appDefaultValue, _ => resolvedSettingValue, "Function1");
            Assert.Equal(expectedLimit, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("2147483648")]
        [InlineData("not-a-number")]
        public void RejectsMissingOrInvalidNamedSettingValue(string resolvedSettingValue)
        {
            var error = Assert.Throws<InvalidOperationException>(() => MaxItemsPerInvocationResolver.Resolve(0, "BatchLimit", "10", _ => resolvedSettingValue, "Function1"));
            Assert.Contains("BatchLimit", error.Message);
            Assert.Contains("Function1", error.Message);
            if (resolvedSettingValue == "not-a-number")
            {
                Assert.DoesNotContain(resolvedSettingValue, error.Message);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("2147483648")]
        [InlineData("invalid")]
        public void InvalidDefaultFailsEvenWithExplicitOverride(string appDefaultValue)
        {
            var error = Assert.Throws<InvalidOperationException>(() => MaxItemsPerInvocationResolver.Resolve(5, null, appDefaultValue, null, "Function1"));
            Assert.Contains("DefaultMaxItemsPerInvocation", error.Message);
        }

        [Fact]
        public void PositiveExplicitValueConflictsWithNamedSetting()
        {
            var error = Assert.Throws<InvalidOperationException>(() => MaxItemsPerInvocationResolver.Resolve(5, "BatchLimit", null, _ => "2", "Function1"));
            Assert.Contains("Function1", error.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void RejectsBlankSettingName(string settingName)
        {
            Assert.Throws<InvalidOperationException>(() => MaxItemsPerInvocationResolver.Resolve(0, settingName, "10", _ => null, "Function1"));
        }
    }
}
