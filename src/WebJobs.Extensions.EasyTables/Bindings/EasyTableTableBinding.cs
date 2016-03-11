// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.EasyTables
{
    /// <summary>
    /// Provides an <see cref="IBinding"/> for valid input table parameters decorated with
    /// an <see cref="EasyTableAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="IMobileServiceTable"/></description></item>
    /// <item><description><see cref="IMobileServiceTable{T}"/>, where T is any Type with a public string Id property</description></item>
    /// </list>
    /// </remarks>
    internal class EasyTableTableBinding : IBinding, IBindingProvider
    {
        private ParameterInfo _parameter;
        private EasyTableContext _context;

        public EasyTableTableBinding(ParameterInfo parameter, EasyTableContext context)
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
            Type paramType = _parameter.ParameterType;
            Type coreType = EasyTableUtility.GetCoreType(paramType);
            if (coreType == typeof(IMobileServiceTable))
            {
                coreType = typeof(JObject);
            }

            return Task.FromResult(CreateTableValueProvider(coreType));
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor
            {
                Name = _parameter.Name
            };
        }

        private IValueProvider CreateTableValueProvider(Type coreType)
        {
            Type genericType = typeof(EasyTableTableValueProvider<>).MakeGenericType(coreType);
            return (IValueProvider)Activator.CreateInstance(genericType, _parameter, _context);
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (IsMobileServiceTableType(context.Parameter.ParameterType))
            {
                return Task.FromResult<IBinding>(this);
            }

            return Task.FromResult<IBinding>(null);
        }

        internal static bool IsMobileServiceTableType(Type paramType)
        {
            if (paramType == typeof(IMobileServiceTable))
            {
                return true;
            }

            if (paramType.IsGenericType &&
                paramType.GetGenericTypeDefinition() == typeof(IMobileServiceTable<>))
            {
                if (EasyTableUtility.IsCoreTypeValidItemType(paramType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}