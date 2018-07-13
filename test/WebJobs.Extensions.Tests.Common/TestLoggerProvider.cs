// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    public class TestLoggerProvider : ILoggerProvider
    {
        private readonly Func<string, LogLevel, bool> _filter;
        private readonly Regex userCategoryRegex = new Regex(@"^Function\.\w+\.User$");

        public IList<TestLogger> CreatedLoggers = new List<TestLogger>();

        public TestLoggerProvider(Func<string, LogLevel, bool> filter = null)
        {
            _filter = filter ?? new LogCategoryFilter().Filter;
        }

        public ILogger CreateLogger(string categoryName)
        {
            var logger = new TestLogger(categoryName, _filter);
            CreatedLoggers.Add(logger);
            return logger;
        }

        public IEnumerable<LogMessage> GetAllLogMessages()
        {
            return CreatedLoggers.SelectMany(l => l.LogMessages);
        }

        public IEnumerable<LogMessage> GetAllUserLogMessages()
        {
            return GetAllLogMessages().Where(p => userCategoryRegex.IsMatch(p.Category));
        }

        public void Dispose()
        {
        }
    }
}
