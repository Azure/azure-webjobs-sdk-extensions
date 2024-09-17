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

            DateTimeOffset now = DateTimeOffset.Now;

            for (int i = 0; i < 10; i++)
            {
                DateTimeOffset nextOccurrence = schedule.GetNextOccurrence(now.LocalDateTime);
                Assert.Equal(new TimeSpan(1, 0, 0), nextOccurrence - now);

                now = nextOccurrence;
            }
        }

        [Fact]
        public void SetNextInterval_OverridesNextInterval()
        {
            ConstantSchedule schedule = new ConstantSchedule(TimeSpan.FromSeconds(30));

            DateTimeOffset now = DateTimeOffset.Now;
            DateTimeOffset nextOccurrence = schedule.GetNextOccurrence(now.LocalDateTime);
            Assert.Equal(new TimeSpan(0, 0, 30), nextOccurrence - now);
            now = nextOccurrence;

            // next interval is overidden
            schedule.SetNextInterval(new TimeSpan(1, 0, 0));
            nextOccurrence = schedule.GetNextOccurrence(now.LocalDateTime);
            Assert.Equal(new TimeSpan(1, 0, 0), nextOccurrence - now);
            now = nextOccurrence;

            // subsequent intervals are not
            nextOccurrence = schedule.GetNextOccurrence(now.LocalDateTime);
            Assert.Equal(new TimeSpan(0, 0, 30), nextOccurrence - now);
            now = nextOccurrence;
        }

        [Fact]
        public void ToString_ReturnsCorrectValue()
        {
            ConstantSchedule schedule = new ConstantSchedule(TimeSpan.FromSeconds(30));

            Assert.Equal("Constant: 00:00:30", schedule.ToString());
        }

        [Fact]
        public void Interval_IntoDST_ReturnsExpectedValue()
        {
            var schedule = new ConstantSchedule(TimeSpan.FromHours(1));

            // Standard -> Daylight occurred on 3/11/2018 at 02:00            
            var start = new DateTime(2018, 3, 10, 23, 30, 0, DateTimeKind.Local);
            TimeZoneInfo pst = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

            TimeSpan offset = pst.GetUtcOffset(start);
            var now = new DateTimeOffset(start, offset);
            schedule.TimeZone = pst;

            var nextOccurrences = schedule.GetNextOccurrences(5, now.LocalDateTime);

            // Cast DateTime to DateTimeOffset so we can see the internally-stored offset details.
            // This is how we internally do all of our calculations.
            Assert.Collection(nextOccurrences,
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 3, 11, 0, 30, 0), TimeSpan.FromHours(-8)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 3, 11, 1, 30, 0), TimeSpan.FromHours(-8)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 3, 11, 3, 30, 0), TimeSpan.FromHours(-7)), o), // offset changes
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 3, 11, 4, 30, 0), TimeSpan.FromHours(-7)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 3, 11, 5, 30, 0), TimeSpan.FromHours(-7)), o));
        }

        [Fact]
        public void Interval_OutOfDST_ReturnsExpectedValue()
        {
            var schedule = new ConstantSchedule(TimeSpan.FromHours(1));

            // Standard -> Daylight occurred on 11/04/2018 at 02:00 (time went back to 01:00)            
            var start = new DateTime(2018, 11, 3, 23, 30, 0, DateTimeKind.Local);
            TimeZoneInfo pst = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

            TimeSpan offset = pst.GetUtcOffset(start);
            var now = new DateTimeOffset(start, offset);
            schedule.TimeZone = pst;

            var nextOccurrences = schedule.GetNextOccurrences(5, now.LocalDateTime);

            // Cast DateTime to DateTimeOffset so we can see the internally-stored offset details.
            // This is how we internally do all of our calculations.
            Assert.Collection(nextOccurrences,
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 0, 30, 0), TimeSpan.FromHours(-7)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 1, 30, 0), TimeSpan.FromHours(-7)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 1, 30, 0), TimeSpan.FromHours(-8)), o), // offset changes
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 2, 30, 0), TimeSpan.FromHours(-8)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 3, 30, 0), TimeSpan.FromHours(-8)), o));
        }
    }
}
