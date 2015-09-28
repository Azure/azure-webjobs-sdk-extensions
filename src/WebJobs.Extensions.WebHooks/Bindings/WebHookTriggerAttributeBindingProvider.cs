// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Framework;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    internal class WebHookTriggerAttributeBindingProvider : ITriggerBindingProvider, IDisposable
    {
        private readonly WebHooksConfiguration _webHooksConfig;
        private WebHookDispatcher _dispatcher;
        private bool disposedValue = false;

        public WebHookTriggerAttributeBindingProvider(WebHooksConfiguration webHooksConfig, WebHookDispatcher dispatcher)
        {
            _webHooksConfig = webHooksConfig;
            _dispatcher = dispatcher;
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            WebHookTriggerAttribute attribute = parameter.GetCustomAttribute<WebHookTriggerAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            // Can bind to user types, HttpRequestMessage, WebHookContext, and all the types supported by StreamValueBinder
            IEnumerable<Type> supportedTypes = StreamValueBinder.SupportedTypes.Union(new Type[] { typeof(HttpRequestMessage), typeof(WebHookContext) });
            bool isSupportedTypeBinding = ValueBinder.MatchParameterType(parameter, supportedTypes);
            bool isUserTypeBinding = !isSupportedTypeBinding && WebHookTriggerBinding.IsValidUserType(parameter.ParameterType);
            if (!isSupportedTypeBinding && !isUserTypeBinding)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    "Can't bind WebHookTriggerAttribute to type '{0}'.", parameter.ParameterType));
            }

            if (!isUserTypeBinding && attribute.FromUri)
            {
                throw new InvalidOperationException("'FromUri' can only be set to True when binding to custom Types.");
            }

            if (!string.IsNullOrEmpty(attribute.Receiver) &&
                !_webHooksConfig.WebHookReceivers.Any(p => string.Compare(p.Name, attribute.Receiver, StringComparison.OrdinalIgnoreCase) == 0))
            {
                throw new InvalidOperationException(string.Format("WebHook receiver '{0}' has not been registered.", attribute.Receiver));
            }

            return Task.FromResult<ITriggerBinding>(new WebHookTriggerBinding(_dispatcher, context.Parameter, isUserTypeBinding, attribute));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_dispatcher != null)
                    {
                        _dispatcher.Dispose();
                        _dispatcher = null;
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}
