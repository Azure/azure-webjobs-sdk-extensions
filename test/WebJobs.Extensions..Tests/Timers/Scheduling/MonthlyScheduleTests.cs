using System;
using WebJobs.Extensions.Timers;
using Xunit;

namespace WebJobs.Extensions.Tests.Timers.Scheduling
{
    public class MonthlyScheduleTests
    {
        [Fact]
        public void GetNextOccurrence_ThreeDaySchedule_MultipleScheduleIterations()
        {
            MonthlySchedule schedule = new MonthlySchedule();

            // simple thrice monthly schedule
            Tuple<int, TimeSpan>[] scheduleData = new Tuple<int, TimeSpan>[]
            {
                new Tuple<int, TimeSpan>(1, new TimeSpan(9, 0, 0)),
                new Tuple<int, TimeSpan>(10, new TimeSpan(18, 0, 0)),
                new Tuple<int, TimeSpan>(20, new TimeSpan(9, 30, 0))
            };

            // set now to be before the first occurrence
            DateTime now = new DateTime(2015, 5, 1, 7, 30, 0);
            VerifySchedule(scheduleData, now);
        }

        [Fact]
        public void GetNextOccurrence_FirstMiddleLastDaySchedule_MultipleScheduleIterations()
        {
            MonthlySchedule schedule = new MonthlySchedule();

            // simple thrice monthly schedule
            Tuple<int, TimeSpan>[] scheduleData = new Tuple<int, TimeSpan>[]
            {
                new Tuple<int, TimeSpan>(1, new TimeSpan(9, 0, 0)),
                new Tuple<int, TimeSpan>(15, new TimeSpan(9, 0, 0)),
                new Tuple<int, TimeSpan>(-1, new TimeSpan(9, 0, 0))
            };

            // set now to be before the first occurrence
            DateTime now = new DateTime(2015, 5, 1, 7, 30, 0);
            VerifySchedule(scheduleData, now);
        }

        private void VerifySchedule(Tuple<int, TimeSpan>[] scheduleData, DateTime now)
        {
            MonthlySchedule schedule = new MonthlySchedule();
            foreach (var occurrence in scheduleData)
            {
                schedule.Add(occurrence.Item1, occurrence.Item2);
            }

            // loop through the full schedule a few times, ensuring we cross over
            // a month boundary ensuring day handling is correct
            for (int i = 0; i < 10; i++)
            {
                // run through the entire schedule once
                for (int j = 0; j < scheduleData.Length; j++)
                {
                    var expectedOccurrence = scheduleData[j];
                    int expectedDay = expectedOccurrence.Item1;
                    if (expectedDay == -1)
                    {
                        expectedDay = DateTime.DaysInMonth(now.Year, now.Month);
                    }

                    DateTime nextOccurrence = schedule.GetNextOccurrence(now); 
                    Assert.Equal(expectedDay, nextOccurrence.Day);
                    Assert.Equal(expectedOccurrence.Item2, nextOccurrence.TimeOfDay);

                    now = nextOccurrence + TimeSpan.FromSeconds(1);
                }
            }
        }
    }
}
