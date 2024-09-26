// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Timers.Scheduling
{
    public class TimerScheduleTests
    {
        private readonly ILogger _logger;
        private readonly TestLoggerProvider _loggerProvider;

        public TimerScheduleTests()
        {
            _loggerProvider = new TestLoggerProvider();
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);
            _logger = _loggerProvider.CreateLogger("Test");
        }

        [Fact]
        public void Create_CronSchedule_CreatesExpectedSchedule()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute("*/15 * * * * *");
            INameResolver nameResolver = new TestNameResolver();
            CronSchedule schedule = (CronSchedule)TimerSchedule.Create(attribute, nameResolver, _logger);
            Assert.False(attribute.UseMonitor);
            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal("UseMonitor changed to false based on schedule frequency.", log.FormattedMessage);
            Assert.Equal(LogLevel.Debug, log.Level);

            DateTime now = new DateTime(2015, 5, 22, 9, 45, 00);
            DateTime nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal(new TimeSpan(0, 0, 15), nextOccurrence - now);

            // For schedules occuring on an interval greater than a minute, we expect
            // UseMonitor to be defaulted to true
            _loggerProvider.ClearAllLogMessages();
            attribute = new TimerTriggerAttribute("0 0 * * * *");
            schedule = (CronSchedule)TimerSchedule.Create(attribute, nameResolver, _logger);
            Assert.True(attribute.UseMonitor);
            Assert.Empty(_loggerProvider.GetAllLogMessages());

            // verify that if UseMonitor is set explicitly, it is not overridden
            _loggerProvider.ClearAllLogMessages();
            attribute = new TimerTriggerAttribute("0 0 * * * *");
            attribute.UseMonitor = false;
            schedule = (CronSchedule)TimerSchedule.Create(attribute, nameResolver, _logger);
            Assert.False(attribute.UseMonitor);
            Assert.Empty(_loggerProvider.GetAllLogMessages());
        }

        [Fact]
        public void Create_ConstantSchedule_CreatesExpectedSchedule()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute("00:00:15");
            INameResolver nameResolver = new TestNameResolver();
            ConstantSchedule schedule = (ConstantSchedule)TimerSchedule.Create(attribute, nameResolver, _logger);
            Assert.False(attribute.UseMonitor);
            var log = _loggerProvider.GetAllLogMessages().Single();
            Assert.Equal("UseMonitor changed to false based on schedule frequency.", log.FormattedMessage);
            Assert.Equal(LogLevel.Debug, log.Level);

            DateTime now = new DateTime(2015, 5, 22, 9, 45, 00);
            DateTime nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal(new TimeSpan(0, 0, 15), nextOccurrence - now);

            // For schedules occuring on an interval greater than a minute, we expect
            // UseMonitor to be defaulted to true
            _loggerProvider.ClearAllLogMessages();
            attribute = new TimerTriggerAttribute("01:00:00");
            schedule = (ConstantSchedule)TimerSchedule.Create(attribute, nameResolver, _logger);
            Assert.True(attribute.UseMonitor);
            Assert.Empty(_loggerProvider.GetAllLogMessages());

            // verify that if UseMonitor is set explicitly, it is not overridden
            attribute = new TimerTriggerAttribute("01:00:00");
            attribute.UseMonitor = false;
            schedule = (ConstantSchedule)TimerSchedule.Create(attribute, nameResolver, _logger);
            Assert.False(attribute.UseMonitor);
            Assert.Empty(_loggerProvider.GetAllLogMessages());
        }

        [Fact]
        public void Create_ConstantSchedule_ScheduleIntervalsAreValid()
        {
            VerifyConstantSchedule("00:00:45", TimeSpan.FromSeconds(45));
            VerifyConstantSchedule("00:06:00", TimeSpan.FromMinutes(6));
            VerifyConstantSchedule("12:00:00", TimeSpan.FromHours(12));
            VerifyConstantSchedule("1.00:00", TimeSpan.FromHours(24));
        }

        private void VerifyConstantSchedule(string expression, TimeSpan expectedInterval)
        {
            Assert.True(TimeSpan.TryParse(expression, out TimeSpan timeSpan));
            Assert.Equal(timeSpan, expectedInterval);

            TimerTriggerAttribute attribute = new TimerTriggerAttribute(expression);
            INameResolver nameResolver = new TestNameResolver();
            ConstantSchedule schedule = (ConstantSchedule)TimerSchedule.Create(attribute, nameResolver, _logger);

            DateTime now = new DateTime(2015, 5, 22, 9, 45, 00);
            var occurrences = schedule.GetNextOccurrences(5, now);

            for (int i = 0; i < 4; i++)
            {
                var delta = occurrences.ElementAt(i + 1) - occurrences.ElementAt(i);
                Assert.Equal(expectedInterval, delta);
            }
        }

        [Fact]
        public void Create_CustomSchedule_CreatesExpectedSchedule()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute(typeof(CustomSchedule));
            INameResolver nameResolver = new TestNameResolver();
            CustomSchedule schedule = (CustomSchedule)TimerSchedule.Create(attribute, nameResolver, _logger);
            Assert.NotNull(schedule);
            Assert.True(attribute.UseMonitor);

            // verify that if UseMonitor is set explicitly, it is not overridden
            attribute = new TimerTriggerAttribute(typeof(CustomSchedule));
            attribute.UseMonitor = false;
            schedule = (CustomSchedule)TimerSchedule.Create(attribute, nameResolver, _logger);
            Assert.False(attribute.UseMonitor);
        }

        [Fact]
        public void Create_UsesNameResolver()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute("%test_schedule%");
            TestNameResolver nameResolver = new TestNameResolver();
            nameResolver.Values.Add("test_schedule", "*/15 * * * * *");
            CronSchedule schedule = (CronSchedule)TimerSchedule.Create(attribute, nameResolver, _logger);
            Assert.False(attribute.UseMonitor);

            DateTime now = new DateTime(2015, 5, 22, 9, 45, 00);
            DateTime nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal(new TimeSpan(0, 0, 15), nextOccurrence - now);
        }

        [Fact]
        public void Create_InvalidSchedule()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute("invalid");
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                TimerSchedule.Create(attribute, new TestNameResolver(), _logger);
            });

            Assert.Equal("The schedule expression 'invalid' was not recognized as a valid cron expression or timespan string.", ex.Message);
        }

        [Theory]
        [InlineData("* * * * * *", null, true)]
        [InlineData("*/5 * * * * *", null, true)]
        [InlineData("  */5    * *      * * *", null, true)]
        [InlineData("10,20,45 * * * * *", null, true)]
        [InlineData("0 */5 * * * *", null, false)]
        [InlineData("0 30 * * * *", null, false)]
        [InlineData("* 30 * * * *", null, false)]
        [InlineData("* */30 * * * *", null, false)]
        [InlineData("0 * * * * *", null, false)]
        [InlineData("30 * * * * *", null, false)]
        [InlineData("* 1-5 * * * *", null, false)]
        [InlineData("* */20 * * * *", "10/30/2020 12:00:00 AM", false)]
        [InlineData("* */20 * * * *", "10/30/2020 12:20:00 AM", false)]
        [InlineData("* */20 * * * *", "10/30/2020 12:15:00 AM", false)]
        [InlineData("* */20 * * * *", "10/30/2020 12:19:30 AM", false)]
        [InlineData("* * 12 * * Mon", null, false)]
        [InlineData("* 59 11 * * 1,2,3,4,5", null, false)]
        public void ShouldDisableScheduleMonitor_ReturnsExpectedValue(string schedule, string nowTimestamp, bool expected)
        {
            DateTime now;
            if (!string.IsNullOrEmpty(nowTimestamp))
            {
                now = DateTime.Parse(nowTimestamp);
            }
            else
            {
                now = DateTime.Now;
            }

            CronSchedule.TryCreate(schedule, out CronSchedule cronSchedule);

            Assert.Equal(expected, TimerSchedule.ShouldDisableScheduleMonitor(cronSchedule, now));
        }

        public class CustomSchedule : TimerSchedule
        {
            public override bool AdjustForDST => true;

            public override DateTime GetNextOccurrence(DateTime now)
            {
                return new DateTime(2015, 5, 22, 9, 45, 00);
            }
        }
    }
}
