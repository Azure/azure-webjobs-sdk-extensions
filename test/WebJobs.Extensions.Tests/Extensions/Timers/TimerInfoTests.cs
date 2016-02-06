// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
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
            TimerInfo timerInfo = new TimerInfo(new CronSchedule("0 0 * * * *"), null);
            string result = timerInfo.FormatNextOccurrences(10, now);

            string expected =
                "The next 10 occurrences of the schedule will be:\r\n" +
                "9/16/2015 11:00:00 AM\r\n" +
                "9/16/2015 12:00:00 PM\r\n" +
                "9/16/2015 1:00:00 PM\r\n" +
                "9/16/2015 2:00:00 PM\r\n" +
                "9/16/2015 3:00:00 PM\r\n" +
                "9/16/2015 4:00:00 PM\r\n" +
                "9/16/2015 5:00:00 PM\r\n" +
                "9/16/2015 6:00:00 PM\r\n" +
                "9/16/2015 7:00:00 PM\r\n" +
                "9/16/2015 8:00:00 PM\r\n";
            Assert.Equal(expected, result);

            timerInfo = new TimerInfo(new DailySchedule("2:00:00"), null);
            result = timerInfo.FormatNextOccurrences(5, now);

            expected =
                "The next 5 occurrences of the schedule will be:\r\n" +
                "9/17/2015 2:00:00 AM\r\n" +
                "9/18/2015 2:00:00 AM\r\n" +
                "9/19/2015 2:00:00 AM\r\n" +
                "9/20/2015 2:00:00 AM\r\n" +
                "9/21/2015 2:00:00 AM\r\n";
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
                "9/16/2015 9:30:00 PM\r\n" +
                "9/18/2015 10:00:00 AM\r\n" +
                "9/21/2015 8:00:00 AM\r\n" +
                "9/23/2015 9:30:00 AM\r\n" +
                "9/23/2015 9:30:00 PM\r\n";
            Assert.Equal(expected, result);
        }
    }
}
