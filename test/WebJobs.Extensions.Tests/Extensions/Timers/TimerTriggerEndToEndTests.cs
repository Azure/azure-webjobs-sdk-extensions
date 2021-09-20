// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers
{
    [Trait("Category", "E2E")]
    public class TimerTriggerEndToEndTests
    {
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

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

            // Make sure we've logged the warning and details about RunOnStartup
            var messages = _loggerProvider.GetAllLogMessages().Where(m => m.FormattedMessage != null);

            // This trigger may be IsPastDue (which is evaluated first) or RunOnStartup. Make sure that we're
            // logging it either way.
            var triggerDetails = messages.Where(m =>
            {
                var msg = m.FormattedMessage;
                return m.Level == LogLevel.Information &&
                       (msg.Contains("Trigger Details: UnscheduledInvocationReason: RunOnStartup") ||
                        msg.Contains("Trigger Details: UnscheduledInvocationReason: IsPastDue"));
            });
            Assert.True(triggerDetails.Count() == 1, string.Join(Environment.NewLine, messages));
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
            var resolver = new TestNameResolver();
            ILoggerFactory loggerFactory = new LoggerFactory();
            TestLoggerProvider provider = new TestLoggerProvider();
            loggerFactory.AddProvider(provider);

            IHost host = new HostBuilder()
                .ConfigureWebJobs(builder =>
                {
                    builder.AddAzureStorageCoreServices()
                    .AddTimers();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IWebJobsExceptionHandler>(new TestExceptionHandler());
                    services.AddSingleton<INameResolver>(resolver);
                    services.AddSingleton<ITypeLocator>(locator);
                    services.AddSingleton<IAzureStorageProvider, TestAzureStorageProvider>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(_loggerProvider);
                })
                .Build();

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

                public override bool AdjustForDST => true;

                public override DateTime GetNextOccurrence(DateTime now)
                {
                    InvocationCount++;
                    return now + TimeSpan.FromSeconds(2);
                }
            }
        }
    }
}
