// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Extensions.Timers.Bindings;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Extensions.Timers
{
    [Extension("Timers")]
    internal class TimersExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly IOptions<TimersOptions> _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly INameResolver _nameResolver;
        private readonly ScheduleMonitor _scheduleMonitor;
        private readonly IDrainModeManager _drainModeManager;

        public TimersExtensionConfigProvider(IOptions<TimersOptions> options, ILoggerFactory loggerFactory,
            INameResolver nameResolver, ScheduleMonitor scheduleMonitor, IDrainModeManager drainModeManager)
        {
            _options = options;
            _loggerFactory = loggerFactory;
            _nameResolver = nameResolver;
            _scheduleMonitor = scheduleMonitor;
            _drainModeManager = drainModeManager;
        }

        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ILogger logger = _loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("Timer"));
            var bindingProvider = new TimerTriggerAttributeBindingProvider(_options.Value, _nameResolver, logger, _scheduleMonitor, _drainModeManager);

            context.AddBindingRule<TimerTriggerAttribute>()
                .BindToTrigger(bindingProvider);
        }
    }
}
