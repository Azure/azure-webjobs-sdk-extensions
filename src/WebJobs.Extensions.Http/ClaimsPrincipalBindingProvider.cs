// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using static Microsoft.Azure.WebJobs.Extensions.Http.HttpTriggerAttributeBindingProvider.HttpTriggerBinding;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    /// <summary>
    /// This provider provides a binding to Type <see cref="ClaimsPrincipal"/>.
    /// </summary>
    internal class ClaimsPrincipalBindingProvider : IBindingProvider
    {
        private static readonly Task<IBinding> NullBinding = Task.FromResult<IBinding>(null);

        private static readonly IReadOnlyCollection<string> PotentialIdentityHeaders = new ReadOnlyCollection<string>(new List<string>()
        {
            "x-ms-client-principal", 
            "x-ms-functions-key-identity"
        });

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (context.Parameter.ParameterType != typeof(ClaimsPrincipal))
            {
                return NullBinding;
            }

            return Task.FromResult<IBinding>(new ClaimsIdentityBinding(context.Parameter));
        }

        private class ClaimsIdentityBinding : IBinding
        {
            private readonly ParameterInfo _parameter;

            public ClaimsIdentityBinding(ParameterInfo parameter)
            {
                _parameter = parameter;
            }

            public bool FromAttribute
            {
                get { return false; }
            }

            public Task<IValueProvider> BindAsync(BindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                IReadOnlyDictionary<string, object> bindingData = context.BindingData;
                HttpRequestMessage requestMessage = bindingData.Values.FirstOrDefault(val => val.GetType() == typeof(HttpRequestMessage)) as HttpRequestMessage;
                ClaimsPrincipal principal = GetClaimsPrincipalFromHttpRequest(requestMessage);
                var valueProvider = new SimpleValueProvider(typeof(ClaimsPrincipal), principal, principal?.Identity?.Name);
                return Task.FromResult<IValueProvider>(valueProvider);
            }

            private static ClaimsPrincipal GetClaimsPrincipalFromHttpRequest(HttpRequestMessage request)
            {
                List<ClaimsIdentity> identities = PotentialIdentityHeaders
                    .Select(header => ClaimsIdentityHelper.GetIdentityFromHttpRequest(request, header))
                    .Where(id => id != null)
                    .ToList();
                return new ClaimsPrincipal(identities);
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                var request = value as ClaimsPrincipal;
                if (request != null)
                {
                    var binding = new SimpleValueProvider(typeof(ClaimsPrincipal), request, "request");
                    return Task.FromResult<IValueProvider>(binding);
                }
                throw new InvalidOperationException("Value must be of type " + typeof(ClaimsPrincipal).ToString());
            }
            
            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor
                {
                    Name = _parameter.Name,
                    DisplayHints = new ParameterDisplayHints
                    {
                        Description = "ClaimsPrincipal"
                    }
                };
            }
        }
    }
}
