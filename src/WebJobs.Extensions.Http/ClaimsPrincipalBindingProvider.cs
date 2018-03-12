// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    /// <summary>
    /// This provider provides a binding to Type <see cref="ClaimsPrincipal"/>.
    /// </summary>
    internal class ClaimsPrincipalBindingProvider : IBindingProvider
    {
        private static Task<IBinding> nullBinding = Task.FromResult<IBinding>(null);

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (context.Parameter.ParameterType != typeof(ClaimsPrincipal))
            {
                return nullBinding;
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
                return Task.FromResult<IValueProvider>(new ClaimsPrincipalValueProvider(ClaimsPrincipalHelper.FromBindingData(bindingData)));
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                throw new NotImplementedException("This method does not provide the necessary binding data.");
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

            private class ClaimsPrincipalValueProvider : IValueProvider
            {
                private readonly ClaimsPrincipal _claimsPrincipal;

                public ClaimsPrincipalValueProvider(ClaimsPrincipal claimsPrincipal)
                {
                    _claimsPrincipal = claimsPrincipal;
                }

                public Type Type
                {
                    get { return typeof(ClaimsPrincipal); }
                }

                public Task<object> GetValueAsync()
                {
                    return Task.FromResult<object>(_claimsPrincipal);
                }

                public string ToInvokeString()
                {
                    // TODO: Decide if this is what we want invoke string to be.
                    return _claimsPrincipal.Identity.Name;
                }
            }
        }
    }
}
