// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Extensions;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    public class TypeExtensionsTests
    {
        [Fact]
        public void GetGenericTypeDisplayNameReturnsExpectedResult()
        {
            Assert.Equal(
                "System.Collections.Generic.IList<TItem>",
                typeof(IList<>).GetGenericTypeDisplayName("TItem"));
        }
    }
}
