using System;
using Microsoft.Azure.WebJobs.Extensions.Extensions.Core;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Core
{
    public class CoreBindingEndToEndTests
    {
        [Fact]
        public void CanBindExecutionContext()
        {
            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = new ExplicitTypeLocator(typeof(CoreTestJobs))
            };
            config.UseCore();
            JobHost host = new JobHost(config);

            host.Call(typeof(CoreTestJobs).GetMethod("ExecutionContext"));

            ExecutionContext result = CoreTestJobs.Context;
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.InvocationId);
        }

        public static class CoreTestJobs
        {
            public static ExecutionContext Context;

            [NoAutomaticTrigger]
            public static void ExecutionContext(ExecutionContext context)
            {
                Context = context;
            }
        }
    }
}
