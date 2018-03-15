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
        private static readonly Task<IBinding> NullBinding = Task.FromResult<IBinding>(null);

        private static readonly ISet<Type> SupportedTypes = new HashSet<Type> { typeof(ClaimsIdentity), typeof(IEnumerable<ClaimsIdentity>) };

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var bindingType = SupportedTypes.FirstOrDefault(type => type == context.Parameter.ParameterType || context.Parameter.ParameterType.IsSubclassOf(type));

            if (bindingType == null)
            {
                return NullBinding;
            }

            return Task.FromResult<IBinding>(new ClaimsIdentityBinding(context.Parameter, bindingType));
        }

        private class ClaimsIdentityBinding : IBinding
        {
            private readonly ParameterInfo _parameter;
            private readonly Type _bindingType;

            public ClaimsIdentityBinding(ParameterInfo parameter, Type bindingType)
            {
                _parameter = parameter;
                _bindingType = bindingType;
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

                var bindingData = context.BindingData;
                var claimsPrincipal = ClaimsPrincipalHelper.FromBindingData(bindingData);

                if (_bindingType == typeof(ClaimsIdentity))
                {
                    if (claimsPrincipal.Identities.Count() == 1)
                    {
                        return Task.FromResult<IValueProvider>(new SingleClaimsIdentityValueProvider(claimsPrincipal.Identities.First()));
                    }

                    var identityProvider = GetIdentityProvider(bindingData);
                    var identity = GetPrimaryIdentity(claimsPrincipal, identityProvider);
                    return Task.FromResult<IValueProvider>(new SingleClaimsIdentityValueProvider(identity));
                }
                else if (_bindingType == typeof(IEnumerable<ClaimsIdentity>))
                {
                    return Task.FromResult<IValueProvider>(new ManyClaimsIdentityValueProvider(claimsPrincipal.Identities));
                }
                else
                {
                    throw new InvalidOperationException($"The type {_bindingType} is not supported");
                }
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                throw new NotImplementedException("This method doesn't have the necessary binding data.");
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

            private static string GetIdentityProvider(IReadOnlyDictionary<string, object> bindingData)
            {
                if (bindingData.TryGetValue(HttpTriggerAttributeBindingProvider.HttpHeadersKey, out object value))
                {
                    IDictionary<string, string> headers = (IDictionary<string, string>)value;
                    if (headers != null)
                    {
                        headers.TryGetValue(HttpConstants.AntaresEasyAuthProviderHeaderName, out string identityProvider);
                        return identityProvider;
                    }
                }
                return null;
            }

            private static ClaimsIdentity GetPrimaryIdentity(ClaimsPrincipal claimsPrincipal, string identityProvider = null)
            {
                // if the principal contains a key based identity that will take precedence
                // over any other identity
                var identity = claimsPrincipal.Identities.LastOrDefault(p => p.IsAuthenticated && string.Equals(p.AuthenticationType, "key", StringComparison.OrdinalIgnoreCase));
                if (identity != null)
                {
                    return identity;
                }

                if (identityProvider != null)
                {
                    foreach (var currIdentity in claimsPrincipal.Identities.Where(p => p.IsAuthenticated))
                    {
                        // if a specific identity provider is specified, look for a match
                        if (string.Equals(currIdentity.AuthenticationType, identityProvider, StringComparison.OrdinalIgnoreCase))
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

            private class SingleClaimsIdentityValueProvider : IValueProvider
            {
                private readonly ClaimsIdentity _identity;

                public SingleClaimsIdentityValueProvider(ClaimsIdentity identity)
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
                    return _identity.Name;
                }
            }

            private class ManyClaimsIdentityValueProvider : IValueProvider
            {
                private readonly IEnumerable<ClaimsIdentity> _identities;

                public ManyClaimsIdentityValueProvider(IEnumerable<ClaimsIdentity> identities)
                {
                    _identities = identities;
                }

                public Type Type => typeof(IEnumerable<ClaimsIdentity>);

                public Task<object> GetValueAsync()
                {
                    return Task.FromResult<object>(_identities);
                }

                public string ToInvokeString()
                {
                    return "{" + string.Join(",", _identities.Select(id => id.Name)) + "}";
                }
            }
        }
    }
}
