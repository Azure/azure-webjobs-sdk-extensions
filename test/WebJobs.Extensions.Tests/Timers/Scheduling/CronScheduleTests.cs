using System;
using NCrontab;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers.Scheduling
{
    public class CronScheduleTests
    {
        [Fact]
        public void GetNextOccurrence_ThreeDaySchedule_MultipleScheduleIterations()
        {
            // 11:59AM on Mondays, Tuesdays, Wednesdays, Thursdays and Fridays
            CronSchedule schedule = new CronSchedule("59 11 * * 1-5");

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
            CronSchedule schedule = new CronSchedule("59 11 * * 1-5");
            Assert.Equal("Cron: '59 11 * * 1-5'", schedule.ToString());
        }
    }
}
