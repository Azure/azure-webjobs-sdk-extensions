// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Warmup.Tests
{
    public class WarmupTriggerEndToEndTests
    {
        private static string _functionOut = null;
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        [Theory]
        [InlineData("WarmupTriggerParams.TestWarmup_String")]
        [InlineData("WarmupTriggerParams.TestWarmup_JObject")]
        [InlineData("WarmupTriggerParams.TestWarmup_WarmupContext")]
        public async Task WarmupTriggerTest_Success(string functionName)
        {
            _functionOut = null;
            var warmupContext = new WarmupContext();

            var args = new Dictionary<string, object>
            {
                { "warmupContext", warmupContext }
            };

            var host = NewHost(types: new Type[] { typeof(WarmupTriggerParams) });

            await host.GetJobHost().CallAsync(functionName, args);
            Assert.Equal(JsonConvert.SerializeObject(warmupContext), _functionOut);
        }

        [Fact]
        public async Task WarmupTriggerTest_Failure()
        {
            _functionOut = null;
            var warmupContext = new WarmupContext();

            var args = new Dictionary<string, object>
            {
                { "warmupContext", warmupContext }
            };

            var host = NewHost(types: new Type[] { typeof(WarmupTriggerParams) });

            // Indexing exceptions will happen in cases where data type for binding is invalid
            host = NewHost(types: new Type[] { typeof(WarmupInvalidBindingParam) });
            var indexException = await Assert.ThrowsAsync<FunctionIndexingException>(() => host.StartAsync());
            Assert.Equal($"Can't bind WarmupTrigger to type '{typeof(int)}'.", indexException.InnerException.Message);
        }

        public IHost NewHost(Type[] types = null)
        {
            IHost host = new HostBuilder()
                .ConfigureDefaultTestHost(builder =>
                {
                   builder.AddWarmup();
                }, types: types)
                .Build();

            return host;
        }

        public class WarmupTriggerParams
        {
            public void TestWarmup_String([WarmupTrigger] string warmupContext)
            {
                _functionOut = warmupContext;
            }

            public void TestWarmup_JObject([WarmupTrigger] JObject warmupContext)
            {
                _functionOut = JsonConvert.SerializeObject(warmupContext);
            }

            public void TestWarmup_WarmupContext([WarmupTrigger] WarmupContext warmupContext)
            {
                _functionOut = JsonConvert.SerializeObject(warmupContext);
            }
        }

        public class WarmupInvalidBindingParam
        {
            public void TestWarmup_Invalid([WarmupTrigger] int warmupContext)
            {
                _functionOut = warmupContext.ToString();
            }
        }
    }
}
