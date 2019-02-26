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
            // There's no way to mock the OS TimeZoneInfo, so let's make sure this 
            // works on both UTC and non-UTC
            string DateFormatter(DateTime d)
            {
                if (TimeZoneInfo.Local == TimeZoneInfo.Utc)
                {
                    return d.ToString(TimerInfo.DateTimeFormat);
                }

                return $"{d.ToString(TimerInfo.DateTimeFormat)} ({d.ToUniversalTime().ToString(TimerInfo.DateTimeFormat)})";
            }

            DateTime now = new DateTime(2015, 9, 16, 10, 30, 00, DateTimeKind.Local);

            CronSchedule cronSchedule = new CronSchedule(CrontabSchedule.Parse("0 * * * *"));
            string result = TimerInfo.FormatNextOccurrences(cronSchedule, 10, now: now);

            var expectedDates = Enumerable.Range(11, 10)
                .Select(hour => new DateTime(2015, 09, 16, hour, 00, 00, DateTimeKind.Local))
                .Select(dateTime => $"{DateFormatter(dateTime)}\r\n")
                .ToArray();

            string expected =
                $"The next 10 occurrences of the schedule ({cronSchedule}) will be:\r\n" +
                string.Join(string.Empty, expectedDates);

            Assert.Equal(expected, result);

            // Test the internal method with timer name specified
            string timerName = "TestTimer";
            TimerSchedule schedule = new DailySchedule("2:00:00");
            result = TimerInfo.FormatNextOccurrences(schedule, 5, now, timerName);

            expectedDates = Enumerable.Range(17, 5)
                .Select(day => new DateTime(2015, 09, day, 02, 00, 00, DateTimeKind.Local))
                .Select(dateTime => $"{DateFormatter(dateTime)}\r\n")
                .ToArray();

            expected =
                    $"The next 5 occurrences of the 'TestTimer' schedule ({schedule}) will be:\r\n" +
                    string.Join(string.Empty, expectedDates);
            Assert.Equal(expected, result);

            WeeklySchedule weeklySchedule = new WeeklySchedule();
            weeklySchedule.Add(DayOfWeek.Monday, new TimeSpan(8, 0, 0));
            weeklySchedule.Add(DayOfWeek.Wednesday, new TimeSpan(9, 30, 0));
            weeklySchedule.Add(DayOfWeek.Wednesday, new TimeSpan(21, 30, 0));
            weeklySchedule.Add(DayOfWeek.Friday, new TimeSpan(10, 0, 0));

            schedule = weeklySchedule;

            result = TimerInfo.FormatNextOccurrences(schedule, 5, now, timerName);

            expected =
                $"The next 5 occurrences of the 'TestTimer' schedule ({weeklySchedule}) will be:\r\n" +
                DateFormatter(new DateTime(2015, 09, 16, 21, 30, 00, DateTimeKind.Local)) + "\r\n" +
                DateFormatter(new DateTime(2015, 09, 18, 10, 00, 00, DateTimeKind.Local)) + "\r\n" +
                DateFormatter(new DateTime(2015, 09, 21, 08, 00, 00, DateTimeKind.Local)) + "\r\n" +
                DateFormatter(new DateTime(2015, 09, 23, 09, 30, 00, DateTimeKind.Local)) + "\r\n" +
                DateFormatter(new DateTime(2015, 09, 23, 21, 30, 00, DateTimeKind.Local)) + "\r\n";

            Assert.Equal(expected, result);
        }
    }
}
