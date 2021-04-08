// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Timers.Scheduling
{
    [Trait("Category", "E2E")]
    public class StorageScheduleMonitorTests : IDisposable
    {
        private const string TestTimerName = "TestProgram.TestTimer";
        private const string TestHostId = "testhostid";
        private readonly StorageScheduleMonitor _scheduleMonitor;

        public StorageScheduleMonitorTests()
        {
            _scheduleMonitor = CreateScheduleMonitor(TestHostId);

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
            StorageScheduleMonitor localScheduleMonitor = CreateScheduleMonitor(null);

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

            var segments = await _scheduleMonitor.TimerStatusDirectory.ListBlobsSegmentedAsync(
                useFlatBlobListing: true,
                blobListingDetails: BlobListingDetails.None,
                maxResults: null,
                currentToken: null,
                options: null,
                operationContext: null);

            var statuses = segments.Results.Cast<CloudBlockBlob>().ToArray();
            Assert.Equal(3, statuses.Length);
            Assert.Equal("timers/testhostid/TestProgram.TestTimer0/status", statuses[0].Name);
            Assert.Equal("timers/testhostid/TestProgram.TestTimer1/status", statuses[1].Name);
            Assert.Equal("timers/testhostid/TestProgram.TestTimer2/status", statuses[2].Name);
        }

        private void Cleanup()
        {
            CloudBlobDirectory directory = _scheduleMonitor.TimerStatusDirectory;
            foreach (CloudBlockBlob blob in directory.ListBlobsSegmentedAsync(
                useFlatBlobListing: true,
                blobListingDetails: BlobListingDetails.None,
                maxResults: null,
                currentToken: null,
                options: null,
                operationContext: null).Result.Results)
            {
                blob.DeleteAsync().Wait();
            }
        }

        public void Dispose()
        {
            Cleanup();
        }

        private static StorageScheduleMonitor CreateScheduleMonitor(string hostId = null)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            var lockContainerManager = new DistributedLockManagerContainerProvider();
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new TestLoggerProvider());

            return new StorageScheduleMonitor(lockContainerManager, new TestIdProvider(hostId), config, loggerFactory);
        }

        private class TestIdProvider : Host.Executors.IHostIdProvider
        {
            private readonly string _hostId;

            public TestIdProvider(string hostId)
            {
                _hostId = hostId;
            }

            public Task<string> GetHostIdAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<string>(_hostId);
            }
        }
    }
}