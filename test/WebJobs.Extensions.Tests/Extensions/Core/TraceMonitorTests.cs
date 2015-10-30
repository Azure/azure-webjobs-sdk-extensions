// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Azure.WebJobs.Host;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Core
{
    public class TraceMonitorTests
    {
        [Fact]
        public void Trace_SubscribersAreNotified()
        {
            int notificationCount = 0;

            var monitor = new TraceMonitor()
                .Filter(p => { return true; })
                .Subscribe(p =>
                {
                    notificationCount++;
                });

            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));
            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));
            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));
            Assert.Equal(3, notificationCount);
        }

        [Fact]
        public void Trace_WithThrottle_NotificationsAreSuspendedThenResumed()
        {
            int notificationCount = 0;
            int filterCount = 0;

            int throttleSeconds = 1;
            var monitor = new TraceMonitor()
                .Filter(p =>
                {
                    filterCount++;
                    return true;
                })
                .Subscribe(p =>
                {
                    notificationCount++;
                })
                .Throttle(TimeSpan.FromSeconds(throttleSeconds));

            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));
            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));
            Assert.Equal(1, notificationCount);

            Thread.Sleep(throttleSeconds * 1000);

            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));
            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));
            Assert.Equal(2, notificationCount);

            Thread.Sleep(throttleSeconds * 1000);

            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));

            Assert.Equal(5, filterCount);
            Assert.Equal(3, notificationCount);
        }

        [Fact]
        public void Trace_IgnoresDuplicateErrors()
        {
            int notificationCount = 0;
            int filterCount = 0;

            var monitor = new TraceMonitor()
                .Filter(p => 
                {
                    filterCount++;
                    return true;
                })
                .Subscribe(p =>
                {
                    notificationCount++;
                });

            Exception ex = new Exception("Kaboom!");

            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!", null, ex));
            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!", null, ex));
            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!", null, ex));
            Assert.Equal(1, filterCount);
            Assert.Equal(1, notificationCount);
        }

        [Fact]
        public void Trace_NoFilters_AlwaysNotify()
        {
            int notificationCount = 0;

            var monitor = new TraceMonitor()
                .Subscribe(p =>
                {
                    notificationCount++;
                });

            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));
            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));
            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));
            Assert.Equal(3, notificationCount);
        }

        [Fact]
        public void Trace_AnonymousFilter_NotifiesAsExpected()
        {
            TraceFilter filter = null;

            var monitor = new TraceMonitor()
                .Filter(p =>
                {
                    return true;
                }, "Custom Message")
                .Subscribe(p =>
                {
                    filter = p;
                });

            TraceEvent traceEvent = new TraceEvent(TraceLevel.Error, "Error!");
            monitor.Trace(traceEvent);

            Assert.Equal("Custom Message", filter.Message);
            Assert.Equal(1, filter.Events.Count);
            Assert.Same(traceEvent, filter.Events.Single());
        }

        [Fact]
        public void Trace_MultipleSubscriptions_AllSubscribersNotified()
        {
            int notificationCount = 0;

            var monitor = new TraceMonitor()
                .Filter(p =>
                {
                    return true;
                })
                .Subscribe(p => { notificationCount++; })
                .Subscribe(p => { notificationCount++; })
                .Subscribe(p => { notificationCount++; });

            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));

            Assert.Equal(3, notificationCount);
        }

        [Fact]
        public void Trace_MultipleFilters_AllFiltersInvoked()
        {
            int filterCount = 0;

            Func<TraceEvent, bool> filter = p =>
            {
                filterCount++;
                return true;
            };

            var monitor = new TraceMonitor()
                .Filter(filter)
                .Filter(filter)
                .Filter(filter);

            monitor.Trace(new TraceEvent(TraceLevel.Error, "Error!"));

            Assert.Equal(3, filterCount);
        }
    }
}
