// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.Warmup.Trigger;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Warmup
{
    [Extension("Warmup")]
    internal class WarmupConfigProvider : IExtensionConfigProvider
    {
        private readonly ILoggerFactory _loggerFactory;

        public WarmupConfigProvider(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var logger = _loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("Warmup"));
            logger.LogInformation("Initializing Warmup Extension.");

            context
                .AddBindingRule<WarmupTriggerAttribute>()
                .AddConverter<WarmupContext, JObject>((w) => JObject.FromObject(w))
                .AddConverter<WarmupContext, string>((w) => JsonConvert.SerializeObject(w))
                .BindToTrigger<WarmupContext>(new WarmupTriggerAttributeBindingProvider());
        }
    }
}
