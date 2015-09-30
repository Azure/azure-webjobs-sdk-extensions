// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    internal class WebHookListener : IListener
    {
        private readonly WebHookDispatcher _dispatcher;
        private readonly ParameterInfo _triggerParameter;
        private readonly Uri _route;
        private readonly ITriggeredFunctionExecutor _executor;

        public WebHookListener(WebHookDispatcher dispatcher, ParameterInfo triggerParameter, Uri route, ITriggeredFunctionExecutor executor)
        {
            _dispatcher = dispatcher;
            _triggerParameter = triggerParameter;
            _route = route;
            _executor = executor;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _dispatcher.RegisterRoute(_route, _triggerParameter, _executor);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _dispatcher.UnregisterRoute(_route);
        }

        public void Dispose()
        {
            _dispatcher.Dispose();
        }

        public void Cancel()
        {
        }
    }
}
