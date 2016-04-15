// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    /// <summary>
    /// Provides an <see cref="IBinding"/> for valid input query parameters decorated with
    /// an <see cref="MobileTableAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The method parameter type must be of Type <see cref="IMobileServiceTableQuery{T}"/>,
    /// where T is any Type with a public string Id property.
    /// </remarks>
    internal class MobileTableQueryBinding : IBinding, IBindingProvider
    {
        private ParameterInfo _parameter;
        private MobileTableContext _context;

        public MobileTableQueryBinding(ParameterInfo parameter, MobileTableContext context)
        {
            _parameter = parameter;
            _context = context;
        }

        public bool FromAttribute
        {
            get { return true; }
        }

        public Task<IValueProvider> BindAsync(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            return BindAsync(null, context.ValueContext);
        }

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            Type genericArgType = null;
            if (TypeUtility.TryGetSingleGenericArgument(_parameter.ParameterType, out genericArgType))
            {
                return Task.FromResult(CreateQueryValueProvider(genericArgType));
            }

            throw new InvalidOperationException("Mobile table parameter types can only have one generic argument.");
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor
            {
                Name = _parameter.Name
            };
        }

        private IValueProvider CreateQueryValueProvider(Type coreType)
        {
            Type genericType = typeof(MobileTableQueryValueProvider<>).MakeGenericType(coreType);
            return (IValueProvider)Activator.CreateInstance(genericType, _parameter, _context);
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (IsValidQueryType(context.Parameter.ParameterType, _context))
            {
                return Task.FromResult<IBinding>(this);
            }

            return Task.FromResult<IBinding>(null);
        }

        internal static bool IsValidQueryType(Type paramType, MobileTableContext context)
        {
            if (paramType.IsGenericType &&
               paramType.GetGenericTypeDefinition() == typeof(IMobileServiceTableQuery<>))
            {
                // IMobileServiceTableQuery<JObject> is not supported.
                Type coreType = TypeUtility.GetCoreType(paramType);
                if (coreType != typeof(JObject) &&
                    MobileAppUtility.IsCoreTypeValidItemType(paramType, context))
                {
                    return true;
                }
            }

            return false;
        }
    }
}