// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.MobileServices;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.MobileApps
{
    public class IMobileServiceExtensionsTests
    {
        [Fact]
        public void AddToTableCache_Throws_IfIncompatible()
        {
            // A single simple test to make sure we throw an InvalidOperationException.
            Assert.Throws<InvalidOperationException>(
                () => IMobileServiceClientExtensions.AddToTableNameCache(new Mock<IMobileServiceClient>().Object, null, string.Empty));
        }
    }
}
