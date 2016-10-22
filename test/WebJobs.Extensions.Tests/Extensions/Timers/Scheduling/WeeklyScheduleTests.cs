// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers.Scheduling
{
    public class WeeklyScheduleTests
    {
        [Fact]
        public void GetNextOccurrence_EmptySchedule_Throws()
        {
            WeeklySchedule schedule = new WeeklySchedule();

            Assert.Throws<InvalidOperationException>(() => schedule.GetNextOccurrence(DateTime.Now));
        }

        [Fact]
        public void GetNextOccurrence_SingleDaySchedule_MultipleScheduleIterations()
        {
            Tuple<DayOfWeek, TimeSpan>[] scheduleData = new Tuple<DayOfWeek, TimeSpan>[]
            {
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Monday, new TimeSpan(9, 0, 0))
            };

            var schedule = new WeeklySchedule();
            foreach (var occurrence in scheduleData)
            {
                schedule.Add(occurrence.Item1, occurrence.Item2);
            }

            // set now to be before the first occurrence
            DateTime now = new DateTime(2015, 5, 23, 7, 30, 0);
            VerifySchedule(scheduleData, now);
        }

        [Fact]
        public void GetNextOccurrence_NowEqualToNext_ReturnsCorrectValue()
        {
            var schedule = new WeeklySchedule();
            schedule.Add(DayOfWeek.Monday, new TimeSpan(9, 0, 0));
            schedule.Add(DayOfWeek.Wednesday, new TimeSpan(18, 0, 0));
            schedule.Add(DayOfWeek.Friday, new TimeSpan(18, 0, 0));

            var now = schedule.GetNextOccurrence(DateTime.Now);
            var next = schedule.GetNextOccurrence(now);

            Assert.True(next > now);
        }

        [Fact]
        public void GetNextOccurrence_ThreeDaySchedule_MultipleScheduleIterations()
        {
            // simple MWF schedule
            Tuple<DayOfWeek, TimeSpan>[] scheduleData = new Tuple<DayOfWeek, TimeSpan>[]
            {
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Monday, new TimeSpan(9, 0, 0)),
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Wednesday, new TimeSpan(18, 0, 0)),
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Friday, new TimeSpan(18, 0, 0))
            };

            // set now to be before the first occurrence
            DateTime now = new DateTime(2015, 5, 23, 7, 30, 0);
            VerifySchedule(scheduleData, now);
        }

        [Fact]
        public void GetNextOccurrence_ComplicatedSchedule_MultipleScheduleIterations()
        {
            // schedule with multiple times per day
            Tuple<DayOfWeek, TimeSpan>[] scheduleData = new Tuple<DayOfWeek, TimeSpan>[]
            {
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Monday, new TimeSpan(8, 0, 0)),
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Monday, new TimeSpan(20, 30, 0)),
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Tuesday, new TimeSpan(12, 0, 0)),
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Wednesday, new TimeSpan(9, 0, 0)),
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Wednesday, new TimeSpan(22, 30, 00)),  // verify schedule self orders
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Wednesday, new TimeSpan(15, 25, 15)),
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Friday, new TimeSpan(9, 30, 0)),
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Saturday, new TimeSpan(5, 30, 0)),
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Saturday, new TimeSpan(17, 30, 0)),
                new Tuple<DayOfWeek, TimeSpan>(DayOfWeek.Sunday, new TimeSpan(10, 00, 0))
            };

            // set now to be before the first occurrence
            DateTime now = new DateTime(2015, 5, 24, 12, 0, 0);
            VerifySchedule(scheduleData, now);
        }

        [Fact]
        public void GetNextOccurrence_Boundaries_ReturnsExpectedNextOccurrence()
        {
            WeeklySchedule schedule = new WeeklySchedule();
            schedule.Add(DayOfWeek.Monday, new TimeSpan(9, 0, 0));
            schedule.Add(DayOfWeek.Wednesday, new TimeSpan(8, 30, 0));
            schedule.Add(DayOfWeek.Wednesday, new TimeSpan(18, 0, 0));
            schedule.Add(DayOfWeek.Friday, new TimeSpan(10, 0, 0));

            // before time on last schedule day - expect last schedule time
            DateTime nextOccurrence = schedule.GetNextOccurrence(DateTime.Parse("2015-05-29T09:59:59"));
            Assert.Equal(DayOfWeek.Friday, nextOccurrence.DayOfWeek);
            Assert.Equal(new TimeSpan(10, 0, 0), nextOccurrence.TimeOfDay);

            // after time on last schedule day - expect advance to beginning of schedule
            nextOccurrence = schedule.GetNextOccurrence(DateTime.Parse("2015-05-29T10:00:01"));
            Assert.Equal(DayOfWeek.Monday, nextOccurrence.DayOfWeek);
            Assert.Equal(new TimeSpan(9, 0, 0), nextOccurrence.TimeOfDay);

            nextOccurrence = schedule.GetNextOccurrence(DateTime.Parse("2015-06-01T08:59:59"));
            Assert.Equal(DayOfWeek.Monday, nextOccurrence.DayOfWeek);
            Assert.Equal(new TimeSpan(9, 0, 0), nextOccurrence.TimeOfDay);

            nextOccurrence = schedule.GetNextOccurrence(DateTime.Parse("2015-06-01T09:00:01"));
            Assert.Equal(DayOfWeek.Wednesday, nextOccurrence.DayOfWeek);
            Assert.Equal(new TimeSpan(8, 30, 0), nextOccurrence.TimeOfDay);

            nextOccurrence = schedule.GetNextOccurrence(DateTime.Parse("2015-06-03T08:30:01"));
            Assert.Equal(DayOfWeek.Wednesday, nextOccurrence.DayOfWeek);
            Assert.Equal(new TimeSpan(18, 00, 0), nextOccurrence.TimeOfDay);

            nextOccurrence = schedule.GetNextOccurrence(DateTime.Parse("2015-06-03T18:00:01"));
            Assert.Equal(DayOfWeek.Friday, nextOccurrence.DayOfWeek);
            Assert.Equal(new TimeSpan(10, 00, 0), nextOccurrence.TimeOfDay);
        }

        [Fact]
        public void ToString_ReturnsCorrectValue()
        {
            WeeklySchedule schedule = new WeeklySchedule();
            schedule.Add(DayOfWeek.Monday, new TimeSpan(9, 0, 0));
            schedule.Add(DayOfWeek.Wednesday, new TimeSpan(8, 30, 0));
            schedule.Add(DayOfWeek.Wednesday, new TimeSpan(18, 0, 0));
            schedule.Add(DayOfWeek.Friday, new TimeSpan(10, 0, 0));

            Assert.Equal("Weekly: 4 occurrences", schedule.ToString());
        }

        private void VerifySchedule(Tuple<DayOfWeek, TimeSpan>[] scheduleData, DateTime now)
        {
            WeeklySchedule schedule = new WeeklySchedule();
            foreach (var occurrence in scheduleData)
            {
                schedule.Add(occurrence.Item1, occurrence.Item2);
            }

            var expectedSchedule = scheduleData.GroupBy(p => p.Item1);

            // loop through the full schedule a few times, ensuring we cross over
            // a month boundary ensuring day handling is correct
            for (int i = 0; i < 10; i++)
            {
                // run through the entire schedule once, ordering the expected times per day
                foreach (var expectedScheduleDay in expectedSchedule)
                {
                    foreach (TimeSpan time in expectedScheduleDay.OrderBy(p => p.Item2).Select(p => p.Item2))
                    {
                        DateTime nextOccurrence = schedule.GetNextOccurrence(now);
                        Assert.Equal(expectedScheduleDay.Key, nextOccurrence.DayOfWeek);
                        Assert.Equal(time, nextOccurrence.TimeOfDay);
                        now = nextOccurrence + TimeSpan.FromSeconds(1);
                    }
                }
            }
        }
    }
}
