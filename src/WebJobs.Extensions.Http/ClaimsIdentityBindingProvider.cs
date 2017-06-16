// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    /// <summary>
    /// This provider provides a binding to Type <see cref="ClaimsIdentity"/>.
    /// </summary>
    /// <remarks>
    /// Attempts to bind to an identity in precedence order based on <see cref="AuthorizationLevel"/> levels.
    /// I.e. if the request is key authenticated (Function/System/Admin) that identity is returned, if the
    /// request is user authenticated (User) that identity is returned.
    /// </remarks>
    internal class ClaimsIdentityBindingProvider : IBindingProvider
    {
        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (context.Parameter.ParameterType != typeof(ClaimsIdentity))
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

            public async Task<IValueProvider> BindAsync(BindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                // if the request is EasyAuth authenticated, get the identity provider type
                object value = null;
                string identityProvider = null;
                if (context.BindingData.TryGetValue(HttpTriggerAttributeBindingProvider.HttpHeadersKey, out value))
                {
                    IDictionary<string, string> headers = (IDictionary<string, string>)value;
                    if (headers != null)
                    {
                        headers.TryGetValue(HttpConstants.AntaresEasyAuthProviderHeaderName, out identityProvider);
                    }  
                }

                var identity = GetPrimaryIdentity(ClaimsPrincipal.Current, identityProvider);
                return await BindInternalAsync(identity);
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                var identity = GetPrimaryIdentity(ClaimsPrincipal.Current);

                return BindInternalAsync(identity);
            }

            private static Task<IValueProvider> BindInternalAsync(ClaimsIdentity identity)
            {
                return Task.FromResult<IValueProvider>(new ClaimsIdentityValueProvider(identity));
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor
                {
                    Name = _parameter.Name,
                    DisplayHints = new ParameterDisplayHints
                    {
                        Description = "ClaimsIdentity"
                    }
                };
            }

            private static ClaimsIdentity GetPrimaryIdentity(ClaimsPrincipal claimsPrincipal, string identityProvider = null)
            {
                // if the principal contains a key based identity that will take precedence
                // over any other identity
                var identity = claimsPrincipal.Identities.LastOrDefault(p => p.IsAuthenticated && string.Compare(p.AuthenticationType, "key", StringComparison.OrdinalIgnoreCase) == 0);
                if (identity != null)
                {
                    return identity;
                }

                if (identityProvider != null)
                {
                    foreach (var currIdentity in claimsPrincipal.Identities.Where(p => p.IsAuthenticated))
                    {
                        // if a specific identity provider is specified, look for a match
                        if (string.Compare(currIdentity.AuthenticationType, identityProvider, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            return currIdentity;
                        }

                        // in client flow scenarios the AuthenticationType will be "Federated", so we must look into the
                        // specific claims for the original identity provider
                        var identityProviderClaim = currIdentity.FindFirst(HttpConstants.AntaresEasyAuthIdentityProviderClaimName);
                        if (identityProviderClaim != null && string.Compare(identityProviderClaim.Value, identityProvider, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            return currIdentity;
                        }
                    }
                }

                // otherwise default to the base primary identity
                return (ClaimsIdentity)claimsPrincipal.Identity;
            }

            private class ClaimsIdentityValueProvider : IValueProvider
            {
                private ClaimsIdentity _identity;

                public ClaimsIdentityValueProvider(ClaimsIdentity identity)
                {
                    _identity = identity;
                }

                public Type Type
                {
                    get { return typeof(ClaimsIdentity); }
                }

                public Task<object> GetValueAsync()
                {
                    return Task.FromResult<object>(_identity);
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
