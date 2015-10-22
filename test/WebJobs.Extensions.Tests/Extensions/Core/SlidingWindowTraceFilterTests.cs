// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Azure.WebJobs.Host;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Core
{
    public class SlidingWindowTraceFilterTests
    {
        [Fact]
        public void Constructor_DefaultsMessage()
        {
            SlidingWindowTraceFilter filter = new SlidingWindowTraceFilter(TimeSpan.FromMinutes(10), 5);
            Assert.Equal("5 events at level 'Error' or lower have occurred within time window 00:10:00.", filter.Message);

            filter = new SlidingWindowTraceFilter(TimeSpan.FromMinutes(10), 5, message: "Custom Message");
            Assert.Equal("Custom Message", filter.Message);

            filter = new SlidingWindowTraceFilter(TimeSpan.FromMinutes(10), 5, level: TraceLevel.Warning);
            Assert.Equal("5 events at level 'Warning' or lower have occurred within time window 00:10:00.", filter.Message);
        }

        [Fact]
        public void RemoveOldEvents_RemovesEventsOutsideWindow()
        {
            SlidingWindowTraceFilter filter = new SlidingWindowTraceFilter(TimeSpan.FromMinutes(10), 5);

            DateTime now = DateTime.UtcNow - TimeSpan.FromMinutes(10);

            Assert.Equal(0, filter.Traces.Count);
            filter.RemoveOldEvents(now);
            Assert.Equal(0, filter.Traces.Count);

            // add some events over a few minutes
            for (int i = 0; i < 10; i++)
            {
                now += TimeSpan.FromMinutes(1);
                var traceEvent = new TraceEvent(TraceLevel.Error, string.Format("Error {0}", i))
                {
                    Timestamp = now
                };
                filter.Traces.Add(traceEvent);
            }

            Assert.Equal(10, filter.Traces.Count);
            filter.RemoveOldEvents(now);
            Assert.Equal(10, filter.Traces.Count);
            Assert.Equal("Error 0", filter.Traces.First().Message);
            Assert.Equal("Error 9", filter.Traces.Last().Message);

            // now advance forward a minute
            now += TimeSpan.FromMinutes(1);
            filter.RemoveOldEvents(now);
            Assert.Equal(9, filter.Traces.Count);
            Assert.Equal("Error 1", filter.Traces.First().Message);
            Assert.Equal("Error 9", filter.Traces.Last().Message);

            // now advance forward a few more minutes
            now += TimeSpan.FromMinutes(5);
            filter.RemoveOldEvents(now);
            Assert.Equal(4, filter.Traces.Count);
            Assert.Equal("Error 6", filter.Traces.First().Message);
            Assert.Equal("Error 9", filter.Traces.Last().Message);

            // finally advance forward past all existing events
            now += TimeSpan.FromMinutes(5);
            filter.RemoveOldEvents(now);
            Assert.Equal(0, filter.Traces.Count);
        }

        [Fact]
        public void Filter_ReturnsTrueWhenThresholdReached()
        {
            SlidingWindowTraceFilter filter = new SlidingWindowTraceFilter(TimeSpan.FromMinutes(10), 3);

            Assert.False(filter.Filter(new TraceEvent(TraceLevel.Error, "Error 1")));
            Assert.False(filter.Filter(new TraceEvent(TraceLevel.Error, "Error 2")));
            Assert.False(filter.Filter(new TraceEvent(TraceLevel.Info, "Error 3")));  // expect this to be ignored based on Level
            Assert.True(filter.Filter(new TraceEvent(TraceLevel.Error, "Error 4")));

            Assert.Equal(3, filter.Traces.Count);
            Assert.Equal("Error 1", filter.Traces[0].Message);
            Assert.Equal("Error 2", filter.Traces[1].Message);
            Assert.Equal("Error 4", filter.Traces[2].Message);
        }

        [Fact]
        public void Filter_AppliesInnerFilter()
        {
            Func<TraceEvent, bool> innerFilter = p => !p.Message.Contains("Ignore");

            SlidingWindowTraceFilter filter = new SlidingWindowTraceFilter(TimeSpan.FromMinutes(10), 3, innerFilter);

            Assert.False(filter.Filter(new TraceEvent(TraceLevel.Error, "Error 1")));
            Assert.False(filter.Filter(new TraceEvent(TraceLevel.Error, "Error 2")));
            Assert.False(filter.Filter(new TraceEvent(TraceLevel.Info, "Error 3")));  // expect this to be ignored based on Level
            Assert.False(filter.Filter(new TraceEvent(TraceLevel.Info, "Error 4 (Ignore)")));  // expect this to be ignored based inner filter
            Assert.True(filter.Filter(new TraceEvent(TraceLevel.Error, "Error 5")));

            Assert.Equal(3, filter.Traces.Count);
            Assert.Equal("Error 1", filter.Traces[0].Message);
            Assert.Equal("Error 2", filter.Traces[1].Message);
            Assert.Equal("Error 5", filter.Traces[2].Message);
        }
    }
}
