// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    /// <summary>
    /// Provides an <see cref="IBinding"/> for valid output parameters decorated with
    /// an <see cref="MobileTableAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="ICollector{T}"/>, where T is either <see cref="JObject"/> or any type with a public string Id property.</description></item>
    /// <item><description><see cref="IAsyncCollector{T}"/>, where T is either <see cref="JObject"/> or any type with a public string Id property.</description></item>
    /// <item><description>out <see cref="JObject"/></description></item>
    /// <item><description>out <see cref="JObject"/>[]</description></item>
    /// <item><description>out T, where T is any Type with a public string Id property</description></item>
    /// <item><description>out T[], where T is any Type with a public string Id property</description></item>
    /// </list>
    /// </remarks>
    internal class MobileTableOutputBindingProvider : IBindingProvider
    {
        private MobileTableContext _mobileTableContext;
        private JobHostConfiguration _jobHostConfig;

        public MobileTableOutputBindingProvider(JobHostConfiguration jobHostConfig, MobileTableContext mobileTableContext)
        {
            _jobHostConfig = jobHostConfig;
            _mobileTableContext = mobileTableContext;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;

            if (IsTypeValid(parameter.ParameterType, _mobileTableContext))
            {
                return CreateBinding(parameter);
            }

            return Task.FromResult<IBinding>(null);
        }

        internal static bool IsTypeValid(Type type, MobileTableContext context)
        {
            bool isValidOut = TypeUtility.IsValidOutType(type, (t) => IsValidMobileTableOutputType(t, context));
            bool isValidCollector = TypeUtility.IsValidCollectorType(type, (t) => IsValidMobileTableOutputType(t, context));

            return isValidOut || isValidCollector;
        }

        internal static bool IsValidMobileTableOutputType(Type paramType, MobileTableContext context)
        {
            // Output bindings also support objects as long as ResolvedTableName is valid
            return MobileAppUtility.IsValidItemType(paramType, context) ||
                (paramType == typeof(object) && !string.IsNullOrEmpty(context.ResolvedTableName));
        }

        internal Task<IBinding> CreateBinding(ParameterInfo parameter)
        {
            Type coreType = TypeUtility.GetCoreType(parameter.ParameterType);

            IBinding genericBinding = BindingFactory.BindGenericCollector(parameter, typeof(MobileTableAsyncCollector<>), coreType,
                _jobHostConfig.GetService<IConverterManager>(), (s) => _mobileTableContext);
            return Task.FromResult(genericBinding);
        }
    }
}