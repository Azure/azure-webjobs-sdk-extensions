// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Core
{
    [Trait("Category", "E2E")]
    public class CoreBindingEndToEndTests
    {
        [Fact]
        public async Task CanBindExecutionContext()
        {
            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = new ExplicitTypeLocator(typeof(CoreTestJobs))
            };
            config.UseCore();
            JobHost host = new JobHost(config);

            string methodName = nameof(CoreTestJobs.ExecutionContext);
            await host.CallAsync(typeof(CoreTestJobs).GetMethod(methodName));

            ExecutionContext result = CoreTestJobs.Context;
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.InvocationId);
            Assert.Equal(methodName, result.FunctionName);
            Assert.Equal(Environment.CurrentDirectory, result.FunctionDirectory);
        }

        public static class CoreTestJobs
        {
            public static ExecutionContext Context { get; set; }

            [NoAutomaticTrigger]
            public static void ExecutionContext(ExecutionContext context)
            {
                Context = context;
            }
        }
    }
}
