// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
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
        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (context.Parameter.ParameterType != typeof(ClaimsPrincipal))
            {
                return Task.FromResult<IBinding>(null);
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

                return BindInternalAsync();
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                return BindInternalAsync();
            }
            
            private static Task<IValueProvider> BindInternalAsync()
            {
                return Task.FromResult<IValueProvider>(new ClaimsPrincipalValueProvider());
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
                public ClaimsPrincipalValueProvider()
                {
                }

                public Type Type
                {
                    get { return typeof(ClaimsPrincipal); }
                }

                public Task<object> GetValueAsync()
                {
                    return Task.FromResult<object>(ClaimsPrincipal.Current);
                }

                public string ToInvokeString()
                {
                    // TODO: figure out right value here
                    return ClaimsPrincipal.Current.ToString();
                }
            }
        }
    }
}
