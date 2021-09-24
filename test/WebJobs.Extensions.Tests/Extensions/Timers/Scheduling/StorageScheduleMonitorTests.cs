// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

            Cleanup().GetAwaiter().GetResult();
        }

        [Fact]
        public void TimerStatusPath_ReturnsExpectedDirectory()
        {
            string path = _scheduleMonitor.TimerStatusPath;
            string expectedPath = string.Format("timers/{0}", TestHostId);
            Assert.Equal(expectedPath, path);
        }

        [Fact]
        public void TimerStatusDirectory_HostIdNull_Throws()
        {
            StorageScheduleMonitor localScheduleMonitor = CreateScheduleMonitor(null);

            string path = null;
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                path = localScheduleMonitor.TimerStatusPath;
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
        public async Task UpdateStatusAsync_MultipleUpdates()
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

            // update the status again
            ScheduleStatus expected2 = new ScheduleStatus
            {
                Last = DateTime.Now.AddMinutes(-10),
                Next = DateTime.Now.AddMinutes(10),
                LastUpdated = DateTime.Now.AddMinutes(-10),
            };
            await _scheduleMonitor.UpdateStatusAsync(TestTimerName, expected2);

            // expect the status to be returned
            status = await _scheduleMonitor.GetStatusAsync(TestTimerName);
            Assert.Equal(expected2.Last, status.Last);
            Assert.Equal(expected2.Next, status.Next);
            Assert.Equal(expected2.LastUpdated, status.LastUpdated);
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

            var blobList = new List<BlobHierarchyItem>();
            var segmentResult = _scheduleMonitor.ContainerClient.GetBlobsByHierarchyAsync(prefix: _scheduleMonitor.TimerStatusPath);
            var asyncEnumerator = segmentResult.GetAsyncEnumerator();

            while (await asyncEnumerator.MoveNextAsync())
            {
                blobList.Add(asyncEnumerator.Current);
            }

            var statuses = blobList.Select(b => b.Blob.Name).ToArray();
            Assert.Equal(3, statuses.Length);
            Assert.Equal("timers/testhostid/TestProgram.TestTimer0/status", statuses[0]);
            Assert.Equal("timers/testhostid/TestProgram.TestTimer1/status", statuses[1]);
            Assert.Equal("timers/testhostid/TestProgram.TestTimer2/status", statuses[2]);
        }

        private async Task Cleanup()
        {
            if (await _scheduleMonitor.ContainerClient.ExistsAsync())
            {
                await foreach (var blobClient in _scheduleMonitor.ContainerClient.GetBlobsAsync(prefix: _scheduleMonitor.TimerStatusPath))
                {
                    await _scheduleMonitor.ContainerClient.DeleteBlobIfExistsAsync(blobClient.Name);
                }
            }
        }

        public void Dispose()
        {
            Cleanup().GetAwaiter().GetResult();
        }

        private static StorageScheduleMonitor CreateScheduleMonitor(string hostId = null)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            IHost tempHost = new HostBuilder()
                .ConfigureServices(services =>
                {
                    // Override configuration
                    services.AddSingleton(config);
                    services.AddAzureStorageCoreServices();
                })
                .ConfigureAppConfiguration(c => c.AddEnvironmentVariables())
                .Build();

            var azureStorageProvider = tempHost.Services.GetRequiredService<IAzureStorageProvider>();
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new TestLoggerProvider());

            return new StorageScheduleMonitor(new TestIdProvider(hostId), loggerFactory, azureStorageProvider);
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
