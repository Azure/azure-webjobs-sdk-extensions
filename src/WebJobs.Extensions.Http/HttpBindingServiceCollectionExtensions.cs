﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    public static class HttpBindingServiceCollectionExtensions
    {
        public static IServiceCollection AddHttpBindingRouting(this IServiceCollection services)
        {
            services.TryAddTransient<IInlineConstraintResolver, DefaultInlineConstraintResolver>();
            services.TryAddSingleton<IWebJobsRouter, WebJobsRouter>();
            return services;
        }
    }
}
