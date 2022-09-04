// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests
{
    public class PublicSurfaceTests
    {
        [Fact]
        public void TimersStoragePublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(StorageScheduleMonitor).Assembly;

            var expected = new[]
            {
                "StorageScheduleMonitor",
                "TimersStorageWebJobsBuilderExtensions",
            };

            JobHostTestHelpers.AssertPublicTypes(expected, assembly);
        }
    }
}
