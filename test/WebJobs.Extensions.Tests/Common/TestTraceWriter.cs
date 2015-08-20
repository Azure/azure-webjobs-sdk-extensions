using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    public class TestTraceWriter : TraceWriter
    {
        public TestTraceWriter() : this(TraceLevel.Verbose)
        {
        }

        public TestTraceWriter(TraceLevel level) : base(level)
        {
            Traces = new Collection<string>();
        }

        public Collection<string> Traces { get; private set; }

        public override void Trace(TraceLevel level, string source, string message, Exception ex)
        {
            string trace = string.Format("{0} {1} {2} {3}", level, source, message, ex);
            Traces.Add(trace);
        }
    }
}
