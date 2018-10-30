// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Timers.Scheduling
{
    public class TimerScheduleTests
    {
        [Fact]
        public void Create_CronSchedule_CreatesExpectedSchedule()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute("*/15 * * * * *");
            INameResolver nameResolver = new TestNameResolver();
            CronSchedule schedule = (CronSchedule)TimerSchedule.Create(attribute, nameResolver);
            Assert.False(attribute.UseMonitor);

            DateTime now = new DateTime(2015, 5, 22, 9, 45, 00);
            DateTime nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal(new TimeSpan(0, 0, 15), nextOccurrence - now);

            // For schedules occuring on an interval greater than a minute, we expect
            // UseMonitor to be defaulted to true
            attribute = new TimerTriggerAttribute("0 0 * * * *");
            schedule = (CronSchedule)TimerSchedule.Create(attribute, nameResolver);
            Assert.True(attribute.UseMonitor);

            // verify that if UseMonitor is set explicitly, it is not overridden
            attribute = new TimerTriggerAttribute("0 0 * * * *");
            attribute.UseMonitor = false;
            schedule = (CronSchedule)TimerSchedule.Create(attribute, nameResolver);
            Assert.False(attribute.UseMonitor);
        }

        [Fact]
        public void Create_ConstantSchedule_CreatesExpectedSchedule()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute("00:00:15");
            INameResolver nameResolver = new TestNameResolver();
            ConstantSchedule schedule = (ConstantSchedule)TimerSchedule.Create(attribute, nameResolver);
            Assert.False(attribute.UseMonitor);

            DateTime now = new DateTime(2015, 5, 22, 9, 45, 00);
            DateTime nextOccurrence = schedule.GetNextOccurrence(now);
            Assert.Equal(new TimeSpan(0, 0, 15), nextOccurrence - now);

            // For schedules occuring on an interval greater than a minute, we expect
            // UseMonitor to be defaulted to true
            attribute = new TimerTriggerAttribute("01:00:00");
            schedule = (ConstantSchedule)TimerSchedule.Create(attribute, nameResolver);
            Assert.True(attribute.UseMonitor);

            // verify that if UseMonitor is set explicitly, it is not overridden
            attribute = new TimerTriggerAttribute("01:00:00");
            attribute.UseMonitor = false;
            schedule = (ConstantSchedule)TimerSchedule.Create(attribute, nameResolver);
            Assert.False(attribute.UseMonitor);
        }

        [Fact]
        public void Create_ConstantSchedule_ScheduleIntervalsAreValid()
        {
            VerifyConstantSchedule("00:00:45", TimeSpan.FromSeconds(45));
            VerifyConstantSchedule("00:06:00", TimeSpan.FromMinutes(6));
            VerifyConstantSchedule("12:00:00", TimeSpan.FromHours(12));
            VerifyConstantSchedule("1.00:00", TimeSpan.FromHours(24));
        }

        private static void VerifyConstantSchedule(string expression, TimeSpan expectedInterval)
        {
            Assert.True(TimeSpan.TryParse(expression, out TimeSpan timeSpan));
            Assert.Equal(timeSpan, expectedInterval);

            TimerTriggerAttribute attribute = new TimerTriggerAttribute(expression);
            INameResolver nameResolver = new TestNameResolver();
            ConstantSchedule schedule = (ConstantSchedule)TimerSchedule.Create(attribute, nameResolver);

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
            CustomSchedule schedule = (CustomSchedule)TimerSchedule.Create(attribute, nameResolver);
            Assert.NotNull(schedule);
            Assert.True(attribute.UseMonitor);

            // verify that if UseMonitor is set explicitly, it is not overridden
            attribute = new TimerTriggerAttribute(typeof(CustomSchedule));
            attribute.UseMonitor = false;
            schedule = (CustomSchedule)TimerSchedule.Create(attribute, nameResolver);
            Assert.False(attribute.UseMonitor);
        }

        [Fact]
        public void Create_UsesNameResolver()
        {
            TimerTriggerAttribute attribute = new TimerTriggerAttribute("%test_schedule%");
            TestNameResolver nameResolver = new TestNameResolver();
            nameResolver.Values.Add("test_schedule", "*/15 * * * * *");
            CronSchedule schedule = (CronSchedule)TimerSchedule.Create(attribute, nameResolver);
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
                TimerSchedule.Create(attribute, new TestNameResolver());
            });

            Assert.Equal("The schedule expression 'invalid' was not recognized as a valid cron expression or timespan string.", ex.Message);
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
