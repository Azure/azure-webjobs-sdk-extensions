using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers.Scheduling
{
    public class DailyScheduleTests
    {
        [Fact]
        public void GetNextOccurrence_SimpleSchedule()
        {
            List<TimeSpan> scheduleData = new List<TimeSpan>
            {
                new TimeSpan(8, 0, 0),
                new TimeSpan(11, 30, 0),
                new TimeSpan(15, 0, 0),
                new TimeSpan(19, 15, 0)
            };

            DateTime now = new DateTime(2015, 5, 23, 7, 30, 0);
            VerifySchedule(scheduleData, now);
        }

        [Fact]
        public void Constructor_TimeStrings()
        {
            DailySchedule schedule = new DailySchedule("08:30:00", "12:00:00", "15:00:00");

            DateTime now = new DateTime(2015, 5, 23, 7, 30, 0);
            DateTime nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal("08:30:00", nextOccurrence.TimeOfDay.ToString());
            now = nextOccurrence + TimeSpan.FromSeconds(1);

            nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal("12:00:00", nextOccurrence.TimeOfDay.ToString());
            now = nextOccurrence + TimeSpan.FromSeconds(1);

            nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal("15:00:00", nextOccurrence.TimeOfDay.ToString());
            now = nextOccurrence + TimeSpan.FromSeconds(1);
        }

        [Fact]
        public void Constructor_TimeSpans()
        {
            DailySchedule schedule = new DailySchedule(
                new TimeSpan(8, 30, 0),
                new TimeSpan(15, 0, 0),  // verify the schedule self orders
                new TimeSpan(12, 00, 0)
            );

            DateTime now = new DateTime(2015, 5, 23, 7, 30, 0);
            DateTime nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal("08:30:00", nextOccurrence.TimeOfDay.ToString());
            now = nextOccurrence + TimeSpan.FromSeconds(1);

            nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal("12:00:00", nextOccurrence.TimeOfDay.ToString());
            now = nextOccurrence + TimeSpan.FromSeconds(1);

            nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal("15:00:00", nextOccurrence.TimeOfDay.ToString());
            now = nextOccurrence + TimeSpan.FromSeconds(1);
        }

        private void VerifySchedule(List<TimeSpan> scheduleData, DateTime now)
        {
            DailySchedule schedule = new DailySchedule(scheduleData.ToArray());

            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < scheduleData.Count; j++)
                {
                    DateTime nextOccurrence = schedule.GetNextOccurrence(now);
                    Assert.Equal(scheduleData[j], nextOccurrence.TimeOfDay);
                    now = nextOccurrence + TimeSpan.FromSeconds(1);
                }
            }
        }
    }
}
