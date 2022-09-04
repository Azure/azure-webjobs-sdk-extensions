// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.Warmup.Trigger
{
    internal class WarmupTriggerBinding : ITriggerBinding
    {
        private readonly ParameterInfo _parameter;

        public WarmupTriggerBinding(ParameterInfo parameter)
        {
            _parameter = parameter;
        }

        public Type TriggerValueType
        {
            get { return typeof(WarmupContext); }
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract => null;

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            return Task.FromResult<ITriggerData>(new TriggerData(null, null));
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return Task.FromResult<IListener>(new NullListener());
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new TriggerParameterDescriptor
            {
                Name = _parameter.Name
            };
        }

        private class NullListener : IListener
        {
            /// <summary>
            /// WarmupTrigger does not need to listen at all.
            /// </summary>
            public NullListener()
            {
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public void Cancel()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
