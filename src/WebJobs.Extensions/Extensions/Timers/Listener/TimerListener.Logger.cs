// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Listeners
{
    internal sealed partial class TimerListener
    {
        private static class Logger
        {
            private static readonly Action<ILogger, string, TimerSchedule, string, Exception> _scheduleAndTimeZone =
                LoggerMessage.Define<string, TimerSchedule, string>(LogLevel.Debug, new EventId(1, nameof(ScheduleAndTimeZone)),
                    "The '{functionName}' timer is using the schedule '{schedule}' and the local time zone: '{timeZone}'");

            private static readonly Action<ILogger, string, string, string, string, Exception> _initialStatus =
                LoggerMessage.Define<string, string, string, string>(LogLevel.Debug, new EventId(2, nameof(InitialStatus)),
                    "Function '{functionName}' initial status: Last='{lastInvoke}', Next='{nextInvoke}', LastUpdated='{lastUpdated}'");

            private static readonly Action<ILogger, string, Exception> _pastDue =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(3, nameof(PastDue)),
                    "Function '{functionName}' is past due on startup. Executing now.");

            private static readonly Action<ILogger, string, Exception> _runOnStartup =
               LoggerMessage.Define<string>(LogLevel.Debug, new EventId(4, nameof(RunOnStartup)),
                   "Function '{functionName}' is configured to run on startup. Executing now.");

            private static readonly Action<ILogger, string, TimerSchedule, string, Exception> _nextOccurrences =
               LoggerMessage.Define<string, TimerSchedule, string>(LogLevel.Information, new EventId(5, nameof(NextOccurrences)),
                   "The next 5 occurrences of the '{functionName}' schedule ({schedule}) will be:{occurrences}");

            private static readonly Action<ILogger, string, TimeSpan, Exception> _timerStarted =
                LoggerMessage.Define<string, TimeSpan>(LogLevel.Debug, new EventId(6, nameof(TimerStarted)),
                    "Timer for '{functionName}' started with interval '{interval}'.");

            private static readonly Action<ILogger, DateTime, string, Exception> _ambiguousTimeAdjustment =
                LoggerMessage.Define<DateTime, string>(LogLevel.Debug, new EventId(7, nameof(AmbiguousTimeAdjustment)),
                    "The time '{ambiguousTime}' is ambiguous in the time zone '{timeZone}' due to Daylight Savings Time. Ignoring time zone offsets to calculate correct interval.");

            public static void ScheduleAndTimeZone(ILogger logger, string functionName, TimerSchedule schedule, string timeZone) =>
                _scheduleAndTimeZone(logger, functionName, schedule, timeZone, null);

            public static void InitialStatus(ILogger logger, string functionName, string lastInvoke, string nextInvoke, string lastUpdated) =>
                _initialStatus(logger, functionName, lastInvoke, nextInvoke, lastUpdated, null);

            public static void PastDue(ILogger logger, string functionName) =>
                _pastDue(logger, functionName, null);

            public static void RunOnStartup(ILogger logger, string functionName) =>
                _runOnStartup(logger, functionName, null);

            public static void NextOccurrences(ILogger logger, string functionName, TimerSchedule schedule, string occurrences) =>
                _nextOccurrences(logger, functionName, schedule, Environment.NewLine + occurrences, null);

            public static void TimerStarted(ILogger logger, string functionName, TimeSpan interval) =>
                _timerStarted(logger, functionName, interval, null);

            public static void AmbiguousTimeAdjustment(ILogger logger, DateTime ambiguousTime, string timeZone) =>
                _ambiguousTimeAdjustment(logger, ambiguousTime, timeZone, null);
        }
    }
}