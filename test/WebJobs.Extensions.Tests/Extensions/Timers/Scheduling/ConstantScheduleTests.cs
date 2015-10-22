// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers.Scheduling
{
    public class ConstantScheduleTests
    {
        [Fact]
        public void GetNextOccurrence_ReturnsExpected()
        {
            ConstantSchedule schedule = new ConstantSchedule(TimeSpan.FromHours(1));

            DateTime now = DateTime.Now;

            for (int i = 0; i < 10; i++)
            {
                DateTime nextOccurrence = schedule.GetNextOccurrence(now);
                Assert.Equal(new TimeSpan(1, 0, 0), nextOccurrence - now);

                now = nextOccurrence;
            }
        }

        [Fact]
        public void SetNextInterval_OverridesNextInterval()
        {
            ConstantSchedule schedule = new ConstantSchedule(TimeSpan.FromSeconds(30));

            DateTime now = DateTime.Now;
            DateTime nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal(new TimeSpan(0, 0, 30), nextOccurrence - now);
            now = nextOccurrence;
            
            // next interval is overidden
            schedule.SetNextInterval(new TimeSpan(1, 0, 0));
            nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal(new TimeSpan(1, 0, 0), nextOccurrence - now);
            now = nextOccurrence;

            // subsequent intervals are not
            nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal(new TimeSpan(0, 0, 30), nextOccurrence - now);
            now = nextOccurrence;
        }

        [Fact]
        public void ToString_ReturnsCorrectValue()
        {
            ConstantSchedule schedule = new ConstantSchedule(TimeSpan.FromSeconds(30));

            Assert.Equal("Constant: 00:00:30", schedule.ToString());
        }
    }
}
