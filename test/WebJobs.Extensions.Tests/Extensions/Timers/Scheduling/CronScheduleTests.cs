// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers.Scheduling
{
    public class CronScheduleTests
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

            DateTime now = new DateTime(2015, 5, 23, 9, 0, 0);

            TimeSpan expectedTime = new TimeSpan(11, 59, 0);
            for (int i = 1; i <= 5; i++)
            {
                DateTime nextOccurrence = schedule.GetNextOccurrence(now);

                Assert.Equal((DayOfWeek)i, nextOccurrence.DayOfWeek);
                Assert.Equal(expectedTime, nextOccurrence.TimeOfDay);
                now = nextOccurrence + TimeSpan.FromSeconds(1);
            }
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
    }
}
