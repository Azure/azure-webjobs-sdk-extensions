// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Host;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Core
{
    public class TraceFilterTests
    {
        [Fact]
        public void FormatMessage_ReturnsExpectedString()
        {
            TraceFilter filter = new TestTraceFilter("WebJob failures detected.");

            for (int i = 0; i < 10; i++)
            {
                filter.Filter(new TraceEvent(TraceLevel.Error, string.Format("Event {0}", i), null, new Exception("Kaboom!")));
            }

            // verify message formatting taking less than the total number of events
            string message = filter.GetDetailedMessage(3);
            string[] messageLines = message.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            Assert.Equal(8, messageLines.Length);
            Assert.Equal("WebJob failures detected.", messageLines[0]);
            Assert.Equal(string.Empty, messageLines[1].Trim());
            Assert.True(messageLines[2].EndsWith("Error Event 9  System.Exception: Kaboom!"));
            Assert.Equal(string.Empty, messageLines[3].Trim());
            Assert.True(messageLines[4].EndsWith("Error Event 8  System.Exception: Kaboom!"));
            Assert.Equal(string.Empty, messageLines[5].Trim());
            Assert.True(messageLines[6].EndsWith("Error Event 7  System.Exception: Kaboom!"));

            // verify message formatting taking greater than the total number of events
            message = filter.GetDetailedMessage(15);
            messageLines = message.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(11, messageLines.Length);

            // test with no events
            filter.Events.Clear();
            message = filter.GetDetailedMessage(3);
            messageLines = message.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            Assert.Equal(2, messageLines.Length);
            Assert.Equal("WebJob failures detected.", messageLines[0]);
            Assert.Equal(string.Empty, messageLines[1].Trim());
        }

        internal class TestTraceFilter : TraceFilter
        {
            private readonly string _message;
            private Collection<TraceEvent> _events = new Collection<TraceEvent>();

            public TestTraceFilter(string message)
            {
                _message = message;
            }

            public override string Message
            {
                get
                {
                    return _message;
                }
            }

            public override Collection<TraceEvent> Events
            {
                get
                {
                    return _events;
                }
            }

            public override bool Filter(TraceEvent traceEvent)
            {
                _events.Add(traceEvent);
                return true;
            }
        }
    }
}
