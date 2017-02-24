// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Timers.Scheduling
{
    public class FileSystemScheduleMonitorTests
    {
        private FileSystemScheduleMonitor _monitor;
        private string _testTimerName;
        private string _statusRoot;
        private string _statusFile;

        public FileSystemScheduleMonitorTests()
        {
            _monitor = new FileSystemScheduleMonitor();
            _testTimerName = "Program.TestJob";
            _statusFile = _monitor.GetStatusFileName(_testTimerName);

            _statusRoot = Path.Combine(Path.GetTempPath(), @"webjobs\timers");
            Directory.CreateDirectory(_statusRoot);
            CleanStatusFiles();
        }

        [Fact]
        public void Constructor_Defaults()
        {
            Environment.SetEnvironmentVariable("HOME", null);

            // when HOME is not defined, default to local temp directory
            FileSystemScheduleMonitor localMonitor = new FileSystemScheduleMonitor();
            string expectedPath = Path.Combine(Path.GetTempPath(), @"webjobs\timers");
            Assert.Equal(expectedPath, localMonitor.StatusFilePath);

            Environment.SetEnvironmentVariable("HOME", @"C:\home");
            string currentDirectory = @"D:\local\Temp\jobs\continuous\Test\mlxx1xht.zmv";  // example from actual Azure WebJob
            string jobDirectory = @"C:\home\data\jobs\continuous\Test";
            Directory.CreateDirectory(jobDirectory);
            localMonitor = new FileSystemScheduleMonitor(currentDirectory);
            Assert.Equal(@"C:\home\data\jobs\continuous\Test", localMonitor.StatusFilePath);
            Directory.Delete(@"C:\home\", true);

            Environment.SetEnvironmentVariable("HOME", null);
        }

        [Fact]
        public void StatusFilePath_OverridesDefaultWhenSet()
        {
            FileSystemScheduleMonitor localMonitor = new FileSystemScheduleMonitor();
            string expectedPath = Path.Combine(Path.GetTempPath(), @"webjobs\timers");
            Assert.Equal(expectedPath, localMonitor.StatusFilePath);
            string statusFileName = localMonitor.GetStatusFileName(_testTimerName);
            Assert.Equal(expectedPath, Path.GetDirectoryName(statusFileName));

            expectedPath = Path.Combine(Path.GetTempPath(), @"webjobstests\anotherstatuspath");
            Directory.CreateDirectory(expectedPath);
            localMonitor.StatusFilePath = expectedPath;
            Assert.Equal(expectedPath, localMonitor.StatusFilePath);
            statusFileName = localMonitor.GetStatusFileName(_testTimerName);
            Assert.Equal(expectedPath, Path.GetDirectoryName(statusFileName));
        }

        [Fact]
        public void StatusFilePath_ThrowsWhenPathDoesNotExist()
        {
            string invalidPath = @"c:\temp\webjobstests\doesnotexist";
            Assert.False(Directory.Exists(invalidPath));

            FileSystemScheduleMonitor localMonitor = new FileSystemScheduleMonitor();
            ArgumentException expectedException =
                Assert.Throws<ArgumentException>(() => localMonitor.StatusFilePath = invalidPath);
            Assert.Equal("value", expectedException.ParamName);
            Assert.Equal("The specified path does not exist.\r\nParameter name: value", expectedException.Message);
        }

        [Fact]
        public async Task UpdateAsync_WritesStatusToFile()
        {
            DateTime now = DateTime.Now;
            DateTime expectedNext = DateTime.Now + TimeSpan.FromMinutes(1);

            File.Delete(_statusFile);
            Assert.False(File.Exists(_statusFile));

            ScheduleStatus status = new ScheduleStatus
            {
                Last = now,
                Next = expectedNext,
                LastUpdated = now
            };
            await _monitor.UpdateStatusAsync(_testTimerName, status);

            Assert.True(File.Exists(_statusFile));
            VerifyScheduleStatus(now, expectedNext, now);

            now = expectedNext;
            expectedNext = now + TimeSpan.FromMinutes(1);
            status = new ScheduleStatus
            {
                Last = now,
                Next = expectedNext,
                LastUpdated = now
            };
            await _monitor.UpdateStatusAsync(_testTimerName, status);
            VerifyScheduleStatus(now, expectedNext, now);
        }

        [Fact]
        public async Task CheckPastDueAsync_NoStatusFile_CreatesInitialStatusFile()
        {
            File.Delete(_statusFile);
            Assert.False(File.Exists(_statusFile));
            DateTime now = DateTime.Now;
            DateTime next = now + TimeSpan.FromDays(5);

            Mock<TimerSchedule> mockSchedule = new Mock<TimerSchedule>(MockBehavior.Strict);
            mockSchedule.Setup(p => p.GetNextOccurrence(It.IsAny<DateTime>())).Returns(next);

            TimeSpan pastDueAmount = await _monitor.CheckPastDueAsync(_testTimerName, now, mockSchedule.Object, null);
            Assert.True(File.Exists(_statusFile));
            VerifyScheduleStatus(default(DateTime), next, now);
            Assert.Equal(TimeSpan.Zero, pastDueAmount);
        }

        [Fact]
        public async Task CheckPastDueAsync_ReturnsExpectedResult()
        {
            Mock<TimerSchedule> mockSchedule = new Mock<TimerSchedule>(MockBehavior.Strict);
            DateTime now = DateTime.Now;
            DateTime next = now + TimeSpan.FromDays(1);
            ScheduleStatus status = new ScheduleStatus
            {
                Last = now,
                Next = next
            };

            mockSchedule.Setup(p => p.GetNextOccurrence(It.IsAny<DateTime>())).Returns(next);
            TimeSpan pastDueAmount = await _monitor.CheckPastDueAsync(_testTimerName, now, mockSchedule.Object, status);
            Assert.Equal(TimeSpan.Zero, pastDueAmount);

            now = now + TimeSpan.FromHours(23);
            pastDueAmount = await _monitor.CheckPastDueAsync(_testTimerName, now, mockSchedule.Object, status);
            Assert.Equal(TimeSpan.Zero, pastDueAmount);

            now = now + TimeSpan.FromHours(1);
            pastDueAmount = await _monitor.CheckPastDueAsync(_testTimerName, now, mockSchedule.Object, status);
            Assert.Equal(TimeSpan.Zero, pastDueAmount);

            now = now + TimeSpan.FromHours(1);
            pastDueAmount = await _monitor.CheckPastDueAsync(_testTimerName, now, mockSchedule.Object, status);
            Assert.Equal(TimeSpan.FromHours(1), pastDueAmount);
        }

        [Fact]
        public async Task CheckPastDueAsync_ScheduleUpdate_UpdatesStatusFile()
        {
            Mock<TimerSchedule> mockSchedule = new Mock<TimerSchedule>(MockBehavior.Strict);
            DateTime now = DateTime.Now;
            DateTime next = now + TimeSpan.FromDays(2);
            ScheduleStatus status = new ScheduleStatus
            {
                Last = now,
                Next = next,
                LastUpdated = now
            };
            await _monitor.UpdateStatusAsync(_testTimerName, status);

            mockSchedule.Setup(p => p.GetNextOccurrence(It.IsAny<DateTime>())).Returns(next);
            TimeSpan pastDueAmount = await _monitor.CheckPastDueAsync(_testTimerName, now, mockSchedule.Object, status);
            Assert.Equal(TimeSpan.Zero, pastDueAmount);

            // now adjust the schedule
            DateTime adjustedNext = next - TimeSpan.FromDays(1);
            mockSchedule.Setup(p => p.GetNextOccurrence(It.IsAny<DateTime>())).Returns(adjustedNext);
            pastDueAmount = await _monitor.CheckPastDueAsync(_testTimerName, now, mockSchedule.Object, status);
            Assert.Equal(TimeSpan.Zero, pastDueAmount);
            ScheduleStatus updatedStatus = await _monitor.GetStatusAsync(_testTimerName);
            Assert.Equal(default(DateTime), updatedStatus.Last);
            Assert.Equal(adjustedNext, updatedStatus.Next);
            Assert.Equal(now, updatedStatus.LastUpdated);

            now = now + TimeSpan.FromHours(23);
            pastDueAmount = await _monitor.CheckPastDueAsync(_testTimerName, now, mockSchedule.Object, status);
            Assert.Equal(TimeSpan.Zero, pastDueAmount);

            now = now + TimeSpan.FromHours(1);
            pastDueAmount = await _monitor.CheckPastDueAsync(_testTimerName, now, mockSchedule.Object, status);
            Assert.Equal(TimeSpan.Zero, pastDueAmount);

            now = now + TimeSpan.FromHours(1);
            pastDueAmount = await _monitor.CheckPastDueAsync(_testTimerName, now, mockSchedule.Object, status);
            Assert.Equal(TimeSpan.FromHours(1), pastDueAmount);
        }

        private void VerifyScheduleStatus(DateTime expectedLast, DateTime expectedNext, DateTime expectedLastUpdated)
        {
            string statusFile = _monitor.GetStatusFileName(_testTimerName);
            string statusLine = File.ReadAllText(statusFile);
            JObject status = JObject.Parse(statusLine);
            DateTime lastOccurrence = (DateTime)status["Last"];
            DateTime nextOccurrence = (DateTime)status["Next"];
            DateTime lastUpdated = (DateTime)status["LastUpdated"];
            Assert.Equal(expectedLast, lastOccurrence);
            Assert.Equal(expectedNext, nextOccurrence);
            Assert.Equal(expectedLastUpdated, lastUpdated);
        }

        private void CleanStatusFiles()
        {
            foreach (string statusFile in Directory.GetFiles(_statusRoot, "*.status"))
            {
                File.Delete(statusFile);
            }
        }
    }
}
