// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Timers.Scheduling
{
    [Trait("Category", "E2E")]
    public class StorageScheduleMonitorTests : IDisposable
    {
        private const string TestTimerName = "TestProgram.TestTimer";
        private const string TestHostId = "testhostid";
        private readonly JobHostConfiguration _hostConfig;
        private readonly StorageScheduleMonitor _scheduleMonitor;

        public StorageScheduleMonitorTests()
        {
            _hostConfig = new JobHostConfiguration();
            _hostConfig.HostId = TestHostId;
            _scheduleMonitor = new StorageScheduleMonitor(_hostConfig, new TestTraceWriter());

            Cleanup();
        }

        [Fact]
        public void TimerStatusDirectory_ReturnsExpectedDirectory()
        {
            CloudBlobDirectory directory = _scheduleMonitor.TimerStatusDirectory;
            string expectedPath = string.Format("timers/{0}/", TestHostId);
            Assert.Equal(expectedPath, directory.Prefix);
        }

        [Fact]
        public void TimerStatusDirectory_HostIdNull_Throws()
        {
            JobHostConfiguration config = new JobHostConfiguration();
            StorageScheduleMonitor localScheduleMonitor = new StorageScheduleMonitor(config, new TestTraceWriter());

            CloudBlobDirectory directory = null;
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                directory = localScheduleMonitor.TimerStatusDirectory;
            });

            Assert.Equal("Unable to determine host ID.", ex.Message);
        }

        [Fact]
        public async Task GetStatusAsync_ReturnsExpectedStatus()
        {
            // no status, so should return null
            ScheduleStatus status = await _scheduleMonitor.GetStatusAsync(TestTimerName);
            Assert.Null(status);

            // update the status
            ScheduleStatus expected = new ScheduleStatus
            {
                Last = DateTime.Now.AddMinutes(-5),
                Next = DateTime.Now.AddMinutes(5),
                LastUpdated = DateTime.Now.AddMinutes(-5),
            };
            await _scheduleMonitor.UpdateStatusAsync(TestTimerName, expected);

            // expect the status to be returned
            status = await _scheduleMonitor.GetStatusAsync(TestTimerName);
            Assert.Equal(expected.Last, status.Last);
            Assert.Equal(expected.Next, status.Next);
            Assert.Equal(expected.LastUpdated, status.LastUpdated);
        }

        [Fact]
        public async Task UpdateStatusAsync_MultipleFunctions()
        {
            // update status for 3 functions
            ScheduleStatus expected = new ScheduleStatus
            {
                Last = DateTime.Now.Subtract(TimeSpan.FromMinutes(5)),
                Next = DateTime.Now.AddMinutes(5)
            };
            for (int i = 0; i < 3; i++)
            {
                await _scheduleMonitor.UpdateStatusAsync(TestTimerName + i.ToString(), expected);
            }

            CloudBlockBlob[] statuses = _scheduleMonitor.TimerStatusDirectory.ListBlobs(useFlatBlobListing: true).Cast<CloudBlockBlob>().ToArray();
            Assert.Equal(3, statuses.Length);
            Assert.Equal("timers/testhostid/TestProgram.TestTimer0/status", statuses[0].Name);
            Assert.Equal("timers/testhostid/TestProgram.TestTimer1/status", statuses[1].Name);
            Assert.Equal("timers/testhostid/TestProgram.TestTimer2/status", statuses[2].Name);
        }

        private void Cleanup()
        {
            CloudBlobDirectory directory = _scheduleMonitor.TimerStatusDirectory;
            foreach (CloudBlockBlob blob in directory.ListBlobs(useFlatBlobListing: true))
            {
                blob.Delete();
            }
        }

        public void Dispose()
        {
            Cleanup();
        }
    }
}
