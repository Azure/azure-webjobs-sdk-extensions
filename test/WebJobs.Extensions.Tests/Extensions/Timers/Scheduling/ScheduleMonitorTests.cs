﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using NCrontab;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Timers.Scheduling
{
    public class ScheduleMonitorTests
    {
        private const string _timerName = "TestTimer";
        private readonly CronSchedule _hourlySchedule;
        private readonly CronSchedule _halfHourlySchedule;
        private readonly CronSchedule _dailySchedule;

        public ScheduleMonitorTests()
        {
            _hourlySchedule = new CronSchedule(CrontabSchedule.Parse("0 * * * *"));
            _halfHourlySchedule = new CronSchedule(CrontabSchedule.Parse("*/30 * * * *"));
            _dailySchedule = new CronSchedule(CrontabSchedule.Parse("0 0 * * *"));
        }

        [Fact]
        public async Task CheckPastDue_NullStatus()
        {
            DateTime now = new DateTime(2017, 1, 1, 9, 35, 0);
            MockScheduleMonitor monitor = new MockScheduleMonitor();

            TimeSpan pastDueAmount = await monitor.CheckPastDueAsync(_timerName, now, _dailySchedule, null);
            Assert.Equal(TimeSpan.Zero, pastDueAmount);
            Assert.Equal(default(DateTime), monitor.CurrentStatus.Last);
            Assert.Equal(DateTimeKind.Local, monitor.CurrentStatus.Last.Kind);
            Assert.Equal(new DateTime(2017, 1, 2), monitor.CurrentStatus.Next);
            Assert.Equal(now, monitor.CurrentStatus.LastUpdated);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public async Task CheckPastDue(bool lastSet, bool lastUpdatedSet)
        {
            DateTime now = DateTime.Parse("1/1/2017 9:35");

            ScheduleStatus status = new ScheduleStatus
            {
                Last = lastSet ? DateTime.Parse("1/1/2017 9:00") : default(DateTime),
                Next = DateTime.Parse("1/1/2017 10:00"),
                LastUpdated = lastUpdatedSet ? DateTime.Parse("1/1/2017 9:00") : default(DateTime)
            };

            MockScheduleMonitor monitor = new MockScheduleMonitor();

            // Check the schedule (simulating a host start without any schedule change). We should not
            // update the status.
            TimeSpan pastDueAmount = await monitor.CheckPastDueAsync(_timerName, now, _hourlySchedule, status);
            Assert.Equal(TimeSpan.Zero, pastDueAmount);
            Assert.Null(monitor.CurrentStatus);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public async Task CheckPastDue_NowPastNext(bool lastSet, bool lastUpdatedSet)
        {
            // Move the time 1 second ahead of 'Next'. We should catch this as past due.
            DateTime now = DateTime.Parse("1/1/2017 10:00:01");

            ScheduleStatus status = new ScheduleStatus
            {
                Last = lastSet ? DateTime.Parse("1/1/2017 9:00") : default(DateTime),
                Next = DateTime.Parse("1/1/2017 10:00"),
                LastUpdated = lastUpdatedSet ? DateTime.Parse("1/1/2017 9:00") : default(DateTime)
            };

            MockScheduleMonitor monitor = new MockScheduleMonitor();

            TimeSpan pastDueAmount = await monitor.CheckPastDueAsync(_timerName, now, _hourlySchedule, status);

            if (lastUpdatedSet || lastSet)
            {
                Assert.Equal(TimeSpan.FromSeconds(1), pastDueAmount);
                Assert.Null(monitor.CurrentStatus);
            }
            else
            {
                // Legacy behavior -- 'LastUpdated' fixed this. The schedule didn't change and we're past due,
                //      but we miss it because there is no 'Last' value, which we require to calculate the 'Next'
                //      value. It also shouldn't register as a schedule change.
                Assert.Equal(TimeSpan.Zero, pastDueAmount);
                Assert.Equal(default(DateTime), monitor.CurrentStatus.Last);
                Assert.Equal(DateTimeKind.Local, monitor.CurrentStatus.Last.Kind);
                Assert.Equal(DateTime.Parse("1/1/2017 11:00"), monitor.CurrentStatus.Next);
                Assert.Equal(now, monitor.CurrentStatus.LastUpdated);
            }
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        private async Task CheckPastDue_ScheduleChange_Longer(bool lastSet, bool lastUpdatedSet)
        {
            DateTime now = DateTime.Parse("1/1/2017 9:35");

            ScheduleStatus status = new ScheduleStatus
            {
                Last = lastSet ? new DateTime(2017, 1, 1, 9, 0, 0) : default(DateTime),
                Next = new DateTime(2017, 1, 1, 10, 0, 0),
                LastUpdated = lastUpdatedSet ? new DateTime(2017, 1, 1, 9, 0, 0) : default(DateTime)
            };

            MockScheduleMonitor monitor = new MockScheduleMonitor();

            // change to daily schedule (status is hourly)
            TimeSpan pastDueAmount = await monitor.CheckPastDueAsync(_timerName, now, _dailySchedule, status);

            Assert.Equal(TimeSpan.Zero, pastDueAmount);

            DateTime expectedNext = new DateTime(2017, 1, 2);
            Assert.Equal(default(DateTime), monitor.CurrentStatus.Last);
            Assert.Equal(DateTimeKind.Local, monitor.CurrentStatus.Last.Kind);
            Assert.Equal(expectedNext, monitor.CurrentStatus.Next);

            if (lastUpdatedSet || lastSet)
            {
                Assert.Equal(new DateTime(2017, 1, 1, 9, 0, 0), monitor.CurrentStatus.LastUpdated);
            }
            else
            {
                // Legacy behavior -- before 'LastUpdated' was added.
                Assert.Equal(now, monitor.CurrentStatus.LastUpdated);
            }
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        private async Task CheckPastDue_ScheduleChange_Shorter(bool lastSet, bool lastUpdatedSet)
        {
            DateTime now = new DateTime(2017, 1, 1, 9, 35, 0);

            ScheduleStatus status = new ScheduleStatus
            {
                Last = lastSet ? new DateTime(2017, 1, 1, 9, 0, 0) : default(DateTime),
                Next = new DateTime(2017, 1, 1, 10, 0, 0),
                LastUpdated = lastUpdatedSet ? new DateTime(2017, 1, 1, 9, 0, 0) : default(DateTime)
            };

            MockScheduleMonitor monitor = new MockScheduleMonitor();

            // Change to half-hour schedule (status is hourly).
            // The 'Next' time calculated by this could be in the past -- so it will be seen as past due
            TimeSpan pastDueAmount = await monitor.CheckPastDueAsync(_timerName, now, _halfHourlySchedule, status);

            if (lastUpdatedSet || lastSet)
            {
                // Because the new time is in the past, we re-calculate it to be the next invocation from 'now'.
                Assert.Equal(TimeSpan.Zero, pastDueAmount);
                Assert.Equal(default(DateTime), monitor.CurrentStatus.Last);
                Assert.Equal(DateTimeKind.Local, monitor.CurrentStatus.Last.Kind);
                Assert.Equal(new DateTime(2017, 1, 1, 10, 0, 0), monitor.CurrentStatus.Next);
                Assert.Equal(now, monitor.CurrentStatus.LastUpdated);
            }
            else
            {
                // Legacy behavior -- before 'LastUpdated' was added.

                // We don't have a 'Last', so we re-calculate from now, which is not past due
                Assert.Equal(TimeSpan.Zero, pastDueAmount);

                // Because the 'Next' times happen to line up, we don't see it as a new schedule and don't update it
                Assert.Null(monitor.CurrentStatus);
            }
        }

        private class MockScheduleMonitor : ScheduleMonitor
        {
            public ScheduleStatus CurrentStatus { get; private set; }

            public override Task<ScheduleStatus> GetStatusAsync(string timerName)
            {
                throw new NotImplementedException();
            }

            public override Task UpdateStatusAsync(string timerName, ScheduleStatus status)
            {
                CurrentStatus = status;
                return Task.FromResult(0);
            }
        }
    }
}
