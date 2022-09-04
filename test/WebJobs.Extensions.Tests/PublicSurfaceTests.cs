// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests
{
    public class PublicSurfaceTests
    {
        [Fact]
        public void ExtensionsPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(TimerTriggerAttribute).Assembly;

            var expected = new[]
            {
                "ConstantSchedule",
                "CronSchedule",
                "DailySchedule",
                "FileAttribute",
                "FileProcessor",
                "FileProcessorFactoryContext",
                "FilesOptions",
                "FilesWebJobsBuilderExtensions",
                "FileSystemScheduleMonitor",
                "FileTriggerAttribute",
                "IFileProcessorFactory",
                "ScheduleMonitor",
                "ScheduleStatus",
                "TimerInfo",
                "TimerWebJobsBuilderExtensions",
                "TimerSchedule",
                "TimersOptions",
                "TimerTriggerAttribute",
                "WeeklySchedule",
                "ExtensionsWebJobsStartup",
                "WarmupContext",
                "WarmupTriggerAttribute",
                "WarmupWebJobsBuilderExtensions"
            };

            JobHostTestHelpers.AssertPublicTypes(expected, assembly);
        }
    }
}
