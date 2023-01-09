// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs.Host.Scale;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    public interface IScaleMonitorFactory
    {
        IScaleMonitor Create(ScaleMonitorContext context);
    }
}
