// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Timers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers
{
    public class TimerTriggerAttributeTests
    {
        [Fact]
        public void Constructor_ScheduleExpression()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute("00:00:15");
            Assert.Equal("00:00:15", attribute.ScheduleExpression);
            Assert.Null(attribute.ScheduleType);
            Assert.True(attribute.UseMonitor);
        }

        [Fact]
        public void Constructor_ScheduleType()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute(typeof(DailySchedule));
            Assert.Null(attribute.ScheduleExpression);
            Assert.Equal(typeof(DailySchedule), attribute.ScheduleType);
            Assert.True(attribute.UseMonitor);
        }
    }
}
