// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace Microsoft.Azure.WebJobs.Extensions.ApplicationInsights
{
    internal sealed class ApplicationInsightsService : IApplicationInsightsService, IDisposable
    {
        private readonly string _instrumentationKey;
        private readonly string _connectionString;

        public ApplicationInsightsService(string instrumentationKey, string connectionString)
        {
            Debugger.Launch();
            _instrumentationKey = instrumentationKey;
            _connectionString = connectionString;
        }

        public void Dispose()
        {
        }
    }
}
