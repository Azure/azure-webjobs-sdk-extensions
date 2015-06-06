using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Timers.Scheduling;
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

            _statusRoot = Path.Combine(Path.GetTempPath(), @"webjobssdk\timers");
            Directory.CreateDirectory(_statusRoot);
            CleanStatusFiles();
        }

        [Fact]
        public void Constructor_Defaults()
        {
            Environment.SetEnvironmentVariable("HOME", null);

            // when HOME is not defined, default to local temp directory
            FileSystemScheduleMonitor localMonitor = new FileSystemScheduleMonitor();
            string expectedPath = Path.Combine(Path.GetTempPath(), @"webjobssdk\timers");
            Assert.Equal(expectedPath, localMonitor.StatusFilePath);

            Environment.SetEnvironmentVariable("HOME", @"C:\home");
            localMonitor = new FileSystemScheduleMonitor();
            Assert.Equal(@"C:\home\data\webjobssdk\timers", localMonitor.StatusFilePath);

            Environment.SetEnvironmentVariable("HOME", null);
        }

        [Fact]
        public void StatusFilePath_OverridesDefaultWhenSet()
        {
            FileSystemScheduleMonitor localMonitor = new FileSystemScheduleMonitor();
            string expectedPath = Path.Combine(Path.GetTempPath(), @"webjobssdk\timers");
            Assert.Equal(expectedPath, localMonitor.StatusFilePath);
            string statusFileName = localMonitor.GetStatusFileName(_testTimerName);
            Assert.Equal(expectedPath, Path.GetDirectoryName(statusFileName));

            expectedPath = Path.Combine(Path.GetTempPath(), @"webjobssdktests\anotherstatuspath");
            Directory.CreateDirectory(expectedPath);
            localMonitor.StatusFilePath = expectedPath;
            Assert.Equal(expectedPath, localMonitor.StatusFilePath);
            statusFileName = localMonitor.GetStatusFileName(_testTimerName);
            Assert.Equal(expectedPath, Path.GetDirectoryName(statusFileName));
        }

        [Fact]
        public void StatusFilePath_ThrowsWhenPathDoesNotExist()
        {
            string invalidPath = @"c:\temp\webjobssdktests\doesnotexist";
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
            DateTime expectedNext = DateTime.UtcNow + TimeSpan.FromMinutes(1);

            Assert.False(File.Exists(_statusFile));

            await _monitor.UpdateAsync(_testTimerName, now, expectedNext);

            Assert.True(File.Exists(_statusFile));
            VerifyScheduleStatus(now, expectedNext);
            
            now = expectedNext;
            expectedNext = now + TimeSpan.FromMinutes(1);
            await _monitor.UpdateAsync(_testTimerName, now, expectedNext);
            VerifyScheduleStatus(now, expectedNext);
        }

        [Fact]
        public async Task IsPastDue_NoStatusFile_CreatesInitialStatusFile()
        {
            Assert.False(File.Exists(_statusFile));
            DateTime now = DateTime.UtcNow;
            DateTime next = now + TimeSpan.FromDays(5);
            bool isPastDue = await _monitor.IsPastDueAsync(_testTimerName, now, next);
            Assert.True(File.Exists(_statusFile));
            VerifyScheduleStatus(default(DateTime), next);
            Assert.False(isPastDue);
        }

        [Fact]
        public async Task IsPastDue_ReturnsExpectedResult()
        {
            DateTime now = DateTime.UtcNow;
            DateTime next = now + TimeSpan.FromDays(1);
            await _monitor.UpdateAsync(_testTimerName, now, next);

            bool isPastDue = await _monitor.IsPastDueAsync(_testTimerName, now, next);
            Assert.False(isPastDue);

            now = now + TimeSpan.FromHours(23);
            isPastDue = await _monitor.IsPastDueAsync(_testTimerName, now, next);
            Assert.False(isPastDue);

            now = now + TimeSpan.FromHours(1);
            isPastDue = await _monitor.IsPastDueAsync(_testTimerName, now, next);
            Assert.False(isPastDue);

            now = now + TimeSpan.FromHours(1);
            isPastDue = await _monitor.IsPastDueAsync(_testTimerName, now, next);
            Assert.True(isPastDue);
        }

        private void VerifyScheduleStatus(DateTime expectedLast, DateTime expectedNext)
        {
            string statusFile = _monitor.GetStatusFileName(_testTimerName);
            string statusLine = File.ReadAllText(statusFile);
            JObject status = JObject.Parse(statusLine);
            DateTime lastOccurrence = (DateTime)status["Last"];
            DateTime nextOccurrence = (DateTime)status["Next"];
            Assert.Equal(expectedLast, lastOccurrence);
            Assert.Equal(expectedNext, nextOccurrence);
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
