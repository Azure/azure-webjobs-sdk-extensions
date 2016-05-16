using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    internal class ApiHubLogger : ILogger
    {
        private TraceWriter _trace;

        public ApiHubLogger(TraceWriter trace)
        {
            _trace = trace;
        }

        public TraceWriter TraceWriter
        {
            get
            {
                return _trace;
            }
        }

        public TraceLevel Level
        {
            get
            {
                return _trace.Level;
            }

            set
            {
                _trace.Level = value;
            }
        }

        public void Error(string message, Exception ex = null, string source = null)
        {
            _trace.Error(message, ex, source);
        }

        public void Info(string message, string source = null)
        {
            _trace.Info(message, source);
        }

        public void Verbose(string message, string source = null)
        {
            _trace.Verbose(message, source);
        }

        public void Warning(string message, string source = null)
        {
            _trace.Warning(message, source);
        }
    }
}
