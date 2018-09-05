// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Parameter.ParameterType != typeof(ClaimsPrincipal))
            {
                return NullBinding;
            }

            return Task.FromResult<IBinding>(new ClaimsPrincipalBinding(context.Parameter));
        }

        private class ClaimsPrincipalBinding : IBinding
        {
            private readonly ParameterInfo _parameter;

            public ClaimsPrincipalBinding(ParameterInfo parameter)
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
                    throw new ArgumentNullException(nameof(context));
                }

                if (!(context.BindingData[HttpTriggerAttributeBindingProvider.RequestBindingName] is HttpRequest request))
                {
                    throw new InvalidOperationException("Cannot bind to ClaimsPrincipal in a non HTTP-triggered function.");
                }
                ClaimsPrincipal principal = request.HttpContext.User;

                var valueProvider = new SimpleValueProvider(typeof(ClaimsPrincipal), principal, principal?.Identity?.Name);
                return Task.FromResult<IValueProvider>(valueProvider);
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                if (value is ClaimsPrincipal principal)
                {
                    var binding = new SimpleValueProvider(typeof(ClaimsPrincipal), principal, "principal");
                    return Task.FromResult<IValueProvider>(binding);
                }
                throw new NotSupportedException($"A parameter of type {nameof(ClaimsPrincipal)} is required to bind to the remote user's identity.");
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
