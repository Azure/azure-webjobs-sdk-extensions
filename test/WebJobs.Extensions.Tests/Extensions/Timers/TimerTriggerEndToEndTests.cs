﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers
{
    [Trait("Category", "E2E")]
    public class TimerTriggerEndToEndTests
    {
        [Fact]
        public async Task CronScheduleJobTest()
        {
            Assert.Equal(0, CronScheduleTestJobs.InvocationCount);

            await RunTimerJobTest(
                typeof(CronScheduleTestJobs),
                () =>
                {
                    return CronScheduleTestJobs.InvocationCount > 5;
                });

            CronScheduleTestJobs.InvocationCount = 0;
        }

        [Fact]
        public async Task ConstantScheduleJobTest()
        {
            Assert.Equal(0, ConstantScheduleTestJobs.InvocationCount);

            await RunTimerJobTest(
                typeof(ConstantScheduleTestJobs),
                () =>
                {
                    return ConstantScheduleTestJobs.InvocationCount > 5;
                });

            ConstantScheduleTestJobs.InvocationCount = 0;
        }

        [Fact]
        public async Task CustomScheduleJobTest()
        {
            Assert.Equal(0, CustomScheduleTestJobs.InvocationCount);

            await RunTimerJobTest(
                typeof(CustomScheduleTestJobs),
                () =>
                {
                    return CustomScheduleTestJobs.InvocationCount > 5;
                });

            Assert.True(CustomScheduleTestJobs.CustomSchedule.InvocationCount >= CustomScheduleTestJobs.InvocationCount);

            CustomScheduleTestJobs.InvocationCount = 0;
        }

        private async Task RunTimerJobTest(Type jobClassType, Func<bool> condition)
        {
            ExplicitTypeLocator locator = new ExplicitTypeLocator(jobClassType);
            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = locator
            };

            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider provider = new TestLoggerProvider();
            loggerFactory.AddProvider(provider);

            config.LoggerFactory = loggerFactory;

            config.AddService<IWebJobsExceptionHandler>(new TestExceptionHandler());
            config.UseTimers();

            JobHost host = new JobHost(config);

            await host.StartAsync();

            await TestHelpers.Await(() =>
            {
                return condition();
            });

            await host.StopAsync();

            // TODO: ensure there were no errors
        }

        public static class CronScheduleTestJobs
        {
            static CronScheduleTestJobs()
            {
                InvocationCount = 0;
            }

            public static int InvocationCount { get; set; }

            public static void EveryTwoSeconds(
                [TimerTrigger("*/2 * * * * *")] TimerInfo timer)
            {
                Assert.NotNull(timer.Schedule);
                Assert.Null(timer.ScheduleStatus);
                InvocationCount++;
            }

            public static void EveryTwoHours(
                [TimerTrigger("0 0 */2 * * *", RunOnStartup = true)] TimerInfo timer)
            {
                Assert.NotNull(timer.Schedule);

                Assert.NotNull(timer.ScheduleStatus);
                DateTime expectedNext = timer.Schedule.GetNextOccurrence(timer.ScheduleStatus.Last);
                Assert.Equal(expectedNext, timer.ScheduleStatus.Next);

                InvocationCount++;
            }
        }

        public static class ConstantScheduleTestJobs
        {
            static ConstantScheduleTestJobs()
            {
                InvocationCount = 0;
            }

            public static int InvocationCount { get; set; }

            public static void EveryTwoSeconds(
                [TimerTrigger("00:00:02")] TimerInfo timer)
            {
                InvocationCount++;
            }
        }

        public static class CustomScheduleTestJobs
        {
            static CustomScheduleTestJobs()
            {
                InvocationCount = 0;
            }

            public static int InvocationCount { get; set; }

            public static void CustomJob(
                [TimerTrigger(typeof(CustomSchedule))] TimerInfo timer)
            {
                InvocationCount++;
            }

            public class CustomSchedule : TimerSchedule
            {
                static CustomSchedule()
                {
                    InvocationCount = 0;
                }

                public static int InvocationCount { get; set; }

                public override DateTime GetNextOccurrence(DateTime now)
                {
                    InvocationCount++;
                    return now + TimeSpan.FromSeconds(2);
                }
            }
        }
    }
}
