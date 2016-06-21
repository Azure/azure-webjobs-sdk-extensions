// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    /// <summary>
    /// Generic Stream provider
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    /// <typeparam name="TFile"></typeparam>
    internal class GenericStreamBindingProvider<TAttribute, TFile> : 
        IBindingProvider, IBindingProvider2
        where TAttribute : Attribute, IFileAttribute
    {
        private readonly Func<TAttribute, Task<TFile>> _strategyBuilder;
        private readonly IConverterManager _converterManager;
        private TraceWriter _trace;

        public GenericStreamBindingProvider(
            Func<TAttribute, Task<TFile>> strategyBuilder,
            IConverterManager converterManager, 
            TraceWriter trace)
        {
            _strategyBuilder = strategyBuilder;
            _converterManager = converterManager;
            _trace = trace;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            // Determine whether we should bind to the current parameter
            ParameterInfo parameter = context.Parameter;
            TAttribute attribute = parameter.GetCustomAttribute<TAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            // Don't bind triggers $$$
            if (attribute.GetType().Name.Contains("Trigger"))
            {
                return Task.FromResult<IBinding>(null);
            }

            return BindDirect(context);
        }
        
        // Called for each instance. 
        // Can be called by the Trigger codepath
        public Task<IBinding> BindDirect(BindingProviderContext context)
        {
            ParameterInfo parameter = context.Parameter;
            TAttribute attribute = parameter.GetCustomAttribute<TAttribute>(inherit: false);

            string path = attribute.Path;

            BindingTemplate bindingTemplate = BindingTemplate.FromString(path, ignoreCase: true);
            bindingTemplate.ValidateContractCompatibility(context.BindingDataContract);

            IBinding binding = null;
            if (parameter.IsOut)
            {
                if (parameter.ParameterType.GetElementType() == typeof(string))
                {
                    // Bind to 'out string'
                    binding = new GenericOutStringFileBinding<TAttribute, TFile>(
                        bindingTemplate, attribute,  _strategyBuilder, _converterManager);
                }
                else
                {
                    binding = new GenericFileBinding<TAttribute, TFile>(parameter, bindingTemplate, attribute, _strategyBuilder, _converterManager);
                }
            }
            else
            {
                // Native
                // Stream
                // TextReader | TextWriter
                // String, byte[]
                binding = new GenericFileBinding<TAttribute, TFile>(parameter, bindingTemplate, attribute, _strategyBuilder, _converterManager);
            }

            if (binding == null)
            {
                _trace.Error("Binding is not supported: " + path);
                throw new InvalidOperationException("Binding is not supported: " + path);
            }

            return Task.FromResult(binding);
        }
    }
}