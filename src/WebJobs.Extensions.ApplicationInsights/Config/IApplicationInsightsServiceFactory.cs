﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.ApplicationInsights
{
    internal interface IApplicationInsightsServiceFactory
    {
        IApplicationInsightsService CreateService(string instrumentationKey, string connectionString);
    }
}
