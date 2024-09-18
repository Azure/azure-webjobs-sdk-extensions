// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers.Scheduling
{
    public class CronScheduleTests : IDisposable
    {
        [Fact]
        public void GetNextOccurrence_NowEqualToNext_ReturnsCorrectValue()
        {
            CronSchedule.TryCreate("0 * * * * *", out CronSchedule schedule);

            var now = schedule.GetNextOccurrence(DateTime.Now);
            var next = schedule.GetNextOccurrence(now);

            Assert.True(next > now);
        }

        [Fact]
        public void GetNextOccurrence_FivePartCronExpression_NowEqualToNext_ReturnsCorrectValue()
        {
            CronSchedule.TryCreate("* * * * *", out CronSchedule schedule);

            var now = schedule.GetNextOccurrence(DateTime.Now);
            var next = schedule.GetNextOccurrence(now);

            Assert.True(next > now);
        }

        [Fact]
        public void GetNextOccurrence_ThreeDaySchedule_MultipleScheduleIterations()
        {
            // 11:59AM on Mondays, Tuesdays, Wednesdays, Thursdays and Fridays
            CronSchedule.TryCreate("0 59 11 * * 1-5", out CronSchedule schedule);

            DateTimeOffset now = new DateTimeOffset(2015, 5, 23, 9, 0, 0, TimeSpan.Zero);

            TimeSpan expectedTime = new TimeSpan(11, 59, 0);
            for (int i = 1; i <= 5; i++)
            {
                DateTimeOffset nextOccurrence = schedule.GetNextOccurrence(now.LocalDateTime);

                Assert.Equal((DayOfWeek)i, nextOccurrence.DayOfWeek);
                Assert.Equal(expectedTime, nextOccurrence.TimeOfDay);
                now = nextOccurrence + TimeSpan.FromSeconds(1);
            }
        }

        [Fact]
        public void Interval_IntoDST_ReturnsExpectedValue()
        {
            TimerListenerTests.SetLocalTimeZoneToPacific();
            var testLogger = new TestLogger("Test");

            // Every hour at the 30 min mark
            CronSchedule.TryCreate("0 30 * * * *", testLogger, out CronSchedule schedule);

            // Standard -> Daylight occurred on 3/11/2018 at 02:00 (time skipped ahead to 3:00)
            var start = new DateTime(2018, 3, 11, 0, 0, 0, DateTimeKind.Local);
            var now = new DateTimeOffset(start);

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
        public void PointInTime_IntoDST_ReturnsExpectedValue()
        {
            TimerListenerTests.SetLocalTimeZoneToPacific();

            // 01:30 every day
            CronSchedule.TryCreate("0 30 1 * * *", out CronSchedule schedule);

            // Standard -> Daylight occurred on 3/11/2018 at 02:00 (time skipped ahead to 3:00)
            var start = new DateTime(2018, 3, 10, 0, 0, 0, DateTimeKind.Local);
            var now = new DateTimeOffset(start);

            var nextOccurrences = schedule.GetNextOccurrences(5, now.LocalDateTime);

            // Cast DateTime to DateTimeOffset so we can see the internally-stored offset details.
            // This is how we internally do all of our calculations.
            Assert.Collection(nextOccurrences,
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 3, 10, 1, 30, 0), TimeSpan.FromHours(-8)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 3, 11, 1, 30, 0), TimeSpan.FromHours(-8)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 3, 12, 1, 30, 0), TimeSpan.FromHours(-7)), o), // offset changes
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 3, 13, 1, 30, 0), TimeSpan.FromHours(-7)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 3, 14, 1, 30, 0), TimeSpan.FromHours(-7)), o));
        }

        [Fact]
        public void PointInTime_WithinAmbiguousHour_ReturnsExpectedValue()
        {
            TimerListenerTests.SetLocalTimeZoneToPacific();

            // every 20 minutes
            CronSchedule.TryCreate("0 */20 * * * *", out CronSchedule schedule);

            // Standard -> Daylight occurred on 11/04/2018 at 02:00 (time went back to 01:00)
            // Ambigous hour is 01:00 - 01:59 as there are two instances in of these in the day.
            var start = new DateTime(2018, 11, 4, 0, 30, 0, DateTimeKind.Local);
            var now = new DateTimeOffset(start);

            // just enough to go fully through the ambiguous time
            var nextOccurrences = schedule.GetNextOccurrences(9, now.LocalDateTime);

            // Cast DateTime to DateTimeOffset so we can see the internally-stored offset details.
            // This is how we internally do all of our calculations.
            Assert.Collection(nextOccurrences,
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 0, 40, 0), TimeSpan.FromHours(-7)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 1, 00, 0), TimeSpan.FromHours(-7)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 1, 20, 0), TimeSpan.FromHours(-7)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 1, 40, 0), TimeSpan.FromHours(-7)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 1, 00, 0), TimeSpan.FromHours(-8)), o), // offset changes
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 1, 20, 0), TimeSpan.FromHours(-8)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 1, 40, 0), TimeSpan.FromHours(-8)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 2, 00, 0), TimeSpan.FromHours(-8)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 2, 20, 0), TimeSpan.FromHours(-8)), o));
        }

        [Fact]
        public void Interval_OutOfDST_ReturnsExpectedValue()
        {
            TimerListenerTests.SetLocalTimeZoneToPacific();
            var logger = new TestLogger("Test");

            // Every hour at the 30 min mark
            CronSchedule.TryCreate("0 30 * * * *", logger, out CronSchedule schedule);

            // Daylight -> Standard occurred on 11/04/2018 at 02:00 (time went back to 01:00)
            var start = new DateTime(2018, 11, 4, 0, 0, 0, DateTimeKind.Local);
            var now = new DateTimeOffset(start);

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

        [Fact]
        public void PointInTime_OutOfDST_ReturnsExpectedValue()
        {
            TimerListenerTests.SetLocalTimeZoneToPacific();

            // 01:30 every day
            CronSchedule.TryCreate("0 30 1 * * *", out CronSchedule schedule);

            // Standard -> Daylight occurred on 11/04/2018 at 02:00 (time went back to 01:00)
            var start = new DateTime(2018, 11, 3, 0, 0, 0, DateTimeKind.Local);
            var now = new DateTimeOffset(start);

            var nextOccurrences = schedule.GetNextOccurrences(5, now.LocalDateTime);

            // Cast DateTime to DateTimeOffset so we can see the internally-stored offset details.
            // This is how we internally do all of our calculations.
            Assert.Collection(nextOccurrences,
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 3, 1, 30, 0), TimeSpan.FromHours(-7)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 4, 1, 30, 0), TimeSpan.FromHours(-7)), o), // only run once when we fall back, even though there are two 1:30s on this day
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 5, 1, 30, 0), TimeSpan.FromHours(-8)), o), // offset changes
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 6, 1, 30, 0), TimeSpan.FromHours(-8)), o),
                o => Assert.Equal(new DateTimeOffset(new DateTime(2018, 11, 7, 1, 30, 0), TimeSpan.FromHours(-8)), o));
        }

        [Fact]
        public void ToString_ReturnsExpectedValue()
        {
            var result = CronSchedule.TryCreate("0 59 11 * * 1-5", out CronSchedule schedule);

            Assert.True(result);
            Assert.Equal("Cron: '0 59 11 * * 1-5'", schedule.ToString());
        }

        [Fact]
        public void NullExpression_ReturnsFalseAndNullcronSchedule()
        {
            var result = CronSchedule.TryCreate(null, out CronSchedule schedule);

            Assert.Null(schedule);
            Assert.False(result);
        }

        [Theory]
        [InlineData("* * * * * *", true)]
        [InlineData("0 * * * * *", true)]
        [InlineData("0 0 * * * *", true)]
        [InlineData("0 0-15 * * * *", true)]
        [InlineData("0 0 * * * 0", true)]
        [InlineData("0 0 1-3 * * 0", true)]
        [InlineData("0 0 0 * * *", false)]
        [InlineData("0 0 0 1 * *", false)]
        [InlineData("0 0 0 1 1 *", false)]
        [InlineData("0 0 0 * * 1", false)]
        [InlineData("* * * * *", true)]
        [InlineData("0 * * * *", true)]
        [InlineData("0-15 * * * *", true)]
        [InlineData("0 * * * 0", true)]
        [InlineData("0 1-3 * * 0", true)]
        [InlineData("0 0 * * *", false)]
        [InlineData("0 0 1 * *", false)]
        [InlineData("0 0 1 1 *", false)]
        [InlineData("0 0 * * 1", false)]
        public void IsInterval_ReturnsExpectedValue(string expression, bool expected)
        {
            CronSchedule.TryCreate(expression, out CronSchedule cronSchedule);

            Assert.Equal(expected, cronSchedule.IsInterval);
        }

        public void Dispose() => TimeZoneInfo.ClearCachedData();
    }
}
