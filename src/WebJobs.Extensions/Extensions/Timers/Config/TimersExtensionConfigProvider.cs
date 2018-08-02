// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Extensions.Timers.Bindings;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Extensions.Timers
{
    internal class TimersExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly IOptions<TimersOptions> _options;
        private readonly IOptions<JobHostOptions> _hostOptions;
        private readonly ILoggerFactory _loggerFactory;
        private readonly INameResolver _nameResolver;
        private readonly DistributedLockManagerContainerProvider _lockContainerProvider;
        private readonly IConnectionStringProvider _connectionStringProvider;

        public TimersExtensionConfigProvider(IOptions<TimersOptions> options, IOptions<JobHostOptions> hostOptions, DistributedLockManagerContainerProvider lockContainerProvider, IConnectionStringProvider connectionStringProvider, ILoggerFactory loggerFactory, INameResolver nameResolver)
        {
            _options = options;
            _hostOptions = hostOptions;
            _loggerFactory = loggerFactory;
            _nameResolver = nameResolver;
            _lockContainerProvider = lockContainerProvider;
            _connectionStringProvider = connectionStringProvider;
        }

        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ILogger logger = _loggerFactory.CreateLogger(LogCategories.CreateTriggerCategory("Timer"));

            if (_options.Value.ScheduleMonitor == null)
            {
                _options.Value.ScheduleMonitor = new StorageScheduleMonitor(_hostOptions.Value, _lockContainerProvider, _connectionStringProvider, logger);
            }

            var bindingProvider = new TimerTriggerAttributeBindingProvider(_options.Value, _nameResolver, logger);
            context.AddBindingRule<TimerTriggerAttribute>()
                .BindToTrigger(bindingProvider);
        }
    }
}
