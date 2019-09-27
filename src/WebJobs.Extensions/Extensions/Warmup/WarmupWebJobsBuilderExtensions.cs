// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Warmup;

namespace Microsoft.Extensions.Hosting
{
    public static class WarmupWebJobsBuilderExtensions
    {
        public static IWebJobsBuilder AddWarmup(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<WarmupConfigProvider>();
            return builder;
        }
    }
}
