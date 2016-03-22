using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    /// <summary>
    /// Generic Stream provider
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    /// <typeparam name="TFile"></typeparam>
    internal class GenerericStreamBindingProvider<TAttribute, TFile> : 
        IBindingProvider, IBindingProvider2
        where TAttribute : Attribute, IFileAttribute
    {
        private readonly Func<TAttribute, Task<TFile>> _strategyBuilder;
        private readonly IConverterManager _converterManager;

        public GenerericStreamBindingProvider(
            Func<TAttribute, Task<TFile>> strategyBuilder,
            IConverterManager converterManager)
        {
            _strategyBuilder = strategyBuilder;
            _converterManager = converterManager;
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
        public async Task<IBinding> BindDirect(BindingProviderContext context)
        {
            ParameterInfo parameter = context.Parameter;
            TAttribute attribute = parameter.GetCustomAttribute<TAttribute>(inherit: false);

            string path = attribute.Path;

            BindingTemplate bindingTemplate = BindingTemplate.FromString(path);
            bindingTemplate.ValidateContractCompatibility(context.BindingDataContract);

            // $$$ Do safety check. 
               
            IBinding binding = null;
            if (parameter.IsOut)
            {
                
                // var func = cm.GetConverter<TUser, TFile>();

                // $$$ Or any conversion?  to TFile?

                if (parameter.ParameterType.GetElementType() == typeof(string))
                {
                    // Bind to 'out string'
                    binding = new GenericOutStringFileBinding<TAttribute, TFile>(
                        bindingTemplate, attribute,  _strategyBuilder, _converterManager);
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
                throw new InvalidOperationException("Can't bind.");
            }

            return binding;
        }
    }

}