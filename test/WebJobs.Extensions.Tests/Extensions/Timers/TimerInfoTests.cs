// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using NCrontab;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers
{
    public class TimerInfoTests : IClassFixture<CultureFixture.EnUs>
    {
        [Fact]
        public void ScheduleStatus_ReturnsExpectedValue()
        {
            TimerSchedule schedule = new ConstantSchedule(TimeSpan.FromDays(1));
            TimerInfo timerInfo = new TimerInfo(schedule, null);
            Assert.Null(timerInfo.ScheduleStatus);

            ScheduleStatus scheduleStatus = new ScheduleStatus();
            timerInfo = new TimerInfo(schedule, scheduleStatus);
            Assert.Same(scheduleStatus, timerInfo.ScheduleStatus);
        }

        [Fact]
        public void FormatNextOccurrences_ReturnsExpectedString()
        {
            DateTime now = new DateTime(2015, 9, 16, 10, 30, 00);
            TimerInfo timerInfo = new TimerInfo(new CronSchedule(CrontabSchedule.Parse("0 * * * *")), null);
            string result = timerInfo.FormatNextOccurrences(10, now);

            var expectedDates = Enumerable.Range(11, 10)
                .Select(hour => new DateTime(2015, 09, 16, hour, 00, 00))
                .Select(dateTime => string.Format("{0}\r\n", dateTime))
                .ToArray();

            string expected =
                "The next 10 occurrences of the schedule will be:\r\n" +
                string.Join(string.Empty, expectedDates);

            Assert.Equal(expected, result);

            timerInfo = new TimerInfo(new DailySchedule("2:00:00"), null);
            result = timerInfo.FormatNextOccurrences(5, now);

            expectedDates = Enumerable.Range(17, 5)
                .Select(day => new DateTime(2015, 09, day, 02, 00, 00))
                .Select(dateTime => string.Format("{0}\r\n", dateTime))
                .ToArray();

            expected =
                "The next 5 occurrences of the schedule will be:\r\n" +
                string.Join(string.Empty, expectedDates);
            Assert.Equal(expected, result);

            WeeklySchedule weeklySchedule = new WeeklySchedule();
            weeklySchedule.Add(DayOfWeek.Monday, new TimeSpan(8, 0, 0));
            weeklySchedule.Add(DayOfWeek.Wednesday, new TimeSpan(9, 30, 0));
            weeklySchedule.Add(DayOfWeek.Wednesday, new TimeSpan(21, 30, 0));
            weeklySchedule.Add(DayOfWeek.Friday, new TimeSpan(10, 0, 0));

            timerInfo = new TimerInfo(weeklySchedule, null);
            
            result = timerInfo.FormatNextOccurrences(5, now);

            expected =
                "The next 5 occurrences of the schedule will be:\r\n" +
                new DateTime(2015, 09, 16, 21, 30, 00).ToString() + "\r\n" +
                new DateTime(2015, 09, 18, 10, 00, 00).ToString() + "\r\n" +
                new DateTime(2015, 09, 21, 08, 00, 00).ToString() + "\r\n" +
                new DateTime(2015, 09, 23, 09, 30, 00).ToString() + "\r\n" +
                new DateTime(2015, 09, 23, 21, 30, 00).ToString() + "\r\n";

            Assert.Equal(expected, result);
        }
    }
}
