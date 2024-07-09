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
            string DateFormatter(DateTime d, TimeZoneInfo tz)
            {
                if (tz == TimeZoneInfo.Utc)
                {
                    return d.ToString(TimerInfo.DateTimeFormat);
                }

                return $"{d.ToString(TimerInfo.DateTimeFormat)} ({d.ToUniversalTime().ToString(TimerInfo.DateTimeFormat)})";
            }

            TimeZoneInfo pst = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            TimeSpan offset = pst.GetUtcOffset(new DateTime(2015, 9, 16, 10, 30, 00));
            DateTimeOffset now = new DateTimeOffset(2015, 9, 16, 10, 30, 00, offset);

            CronSchedule cronSchedule = new CronSchedule(CrontabSchedule.Parse("0 * * * *"));
            string result = TimerInfo.FormatNextOccurrences(cronSchedule, 10, now: now.DateTime, pst);

            var expectedDates = Enumerable.Range(11, 10)
                .Select(hour => new DateTime(2015, 09, 16, hour, 00, 00))
                .Select(dateTime => $"{DateFormatter(dateTime, pst)}\r\n")
                .ToArray();

            string expected = string.Join(string.Empty, expectedDates);

            Assert.Equal(expected, result);

            // Test the internal method with timer name specified
            TimerSchedule schedule = new DailySchedule("2:00:00");
            result = TimerInfo.FormatNextOccurrences(schedule, 5, now.DateTime, pst);

            expectedDates = Enumerable.Range(17, 5)
                .Select(day => new DateTime(2015, 09, day, 02, 00, 00))
                .Select(dateTime => $"{DateFormatter(dateTime, pst)}\r\n")
                .ToArray();

            expected = string.Join(string.Empty, expectedDates);
            Assert.Equal(expected, result);

            WeeklySchedule weeklySchedule = new WeeklySchedule();
            weeklySchedule.Add(DayOfWeek.Monday, new TimeSpan(8, 0, 0));
            weeklySchedule.Add(DayOfWeek.Wednesday, new TimeSpan(9, 30, 0));
            weeklySchedule.Add(DayOfWeek.Wednesday, new TimeSpan(21, 30, 0));
            weeklySchedule.Add(DayOfWeek.Friday, new TimeSpan(10, 0, 0));

            schedule = weeklySchedule;

            result = TimerInfo.FormatNextOccurrences(schedule, 5, now.DateTime, pst);

            expected =
                DateFormatter(new DateTime(2015, 09, 16, 21, 30, 00), pst) + "\r\n" +
                DateFormatter(new DateTime(2015, 09, 18, 10, 00, 00), pst) + "\r\n" +
                DateFormatter(new DateTime(2015, 09, 21, 08, 00, 00), pst) + "\r\n" +
                DateFormatter(new DateTime(2015, 09, 23, 09, 30, 00), pst) + "\r\n" +
                DateFormatter(new DateTime(2015, 09, 23, 21, 30, 00), pst) + "\r\n";

            Assert.Equal(expected, result);
        }
    }
}
