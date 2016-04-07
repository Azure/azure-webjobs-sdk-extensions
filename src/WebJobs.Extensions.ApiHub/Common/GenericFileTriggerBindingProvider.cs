using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    internal class GenericFileTriggerBindingProvider<TAttribute, TFile> : ITriggerBindingProvider
          where TAttribute : Attribute, IFileAttribute
    {
        private readonly Func<TAttribute, ITriggeredFunctionExecutor, Task<IListener>> _listenerBuilder;
        private readonly IBindingProvider2 _provider; // for regular binding to objects. 
        private readonly IFileTriggerStrategy<TFile> _strategy;

        public GenericFileTriggerBindingProvider(
            Func<TAttribute, ITriggeredFunctionExecutor, Task<IListener>> listenerBuilder,
            IBindingProvider2 provider,
            IFileTriggerStrategy<TFile> strategy)
        {
            this._listenerBuilder = listenerBuilder;
            this._provider = provider;
            this._strategy = strategy;
        }

        // Called once at each parameter. 
        public async Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            // Determine whether we should bind to the current parameter
            ParameterInfo parameter = context.Parameter;
            var attribute = parameter.GetCustomAttribute<TAttribute>(inherit: false);
            if (attribute == null)
            {
                return null;
            }

            string path = attribute.Path;

            var binding = new GenericTriggerbinding(parameter, attribute, this);

            var bindingContract = binding.BindingDataContract;
            BindingProviderContext bindingContext2 = new BindingProviderContext(parameter, bindingContract, context.CancellationToken);
            var regularBinding = await _provider.BindDirect(bindingContext2);

            binding._binding = regularBinding;
            
            return binding;
        }

        // 1 instance of the binding per parameter
        class GenericTriggerbinding : ITriggerBinding
        {
            private readonly TAttribute _attribute;
            private readonly GenericFileTriggerBindingProvider<TAttribute, TFile> _parent;
            internal  IBinding _binding;
            private readonly IReadOnlyDictionary<string, Type> _bindingContract;
            private readonly BindingDataProvider _bindingDataProvider;
            private readonly ParameterInfo _parameter;

            public GenericTriggerbinding(
                ParameterInfo parameter,
                TAttribute attribute, 
                GenericFileTriggerBindingProvider<TAttribute, TFile> parent)
            {
                this._parameter = parameter;
                this._attribute = attribute;
                this._parent = parent;

                _bindingDataProvider = BindingDataProvider.FromTemplate(_attribute.Path);
                _bindingContract = CreateBindingContract();
            }

            private IReadOnlyDictionary<string, Type> CreateBindingContract()
            {
                Dictionary<string, Type> contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

                _parent._strategy.GetStaticBindingContract(contract);

                if (_bindingDataProvider.Contract != null)
                {
                    foreach (KeyValuePair<string, Type> item in _bindingDataProvider.Contract)
                    {
                        // In case of conflict, binding data from the value type overrides the built-in binding data above.
                        contract[item.Key] = item.Value;
                    }
                }

                return contract;
            }

            public IReadOnlyDictionary<string, Type> BindingDataContract
            {
                get
                {
                    return _bindingContract;
                }
            }

            public Type TriggerValueType
            {
                get
                {
                    return typeof(TFile);
                }
            }

            public async Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                TFile file = (TFile)value;
                var bindingData = GetBindingData(file);
                string path = _parent._strategy.GetPath(file);

                // generic binder binds on a Path as string 
                var valueProvider = await _binding.BindAsync(path, context);

                ITriggerData data = new TriggerData(valueProvider, bindingData);
                return data;
            }

            private IReadOnlyDictionary<string, object> GetBindingData(TFile file)
            {
                string path = _parent._strategy.GetPath(file);

                var dict = GetBindingData(path);

                _parent._strategy.GetRuntimeBindingContract(file, dict);
                return dict;
            }

            private Dictionary<string, object> GetBindingData(string path)
            {
                if (path == null)
                {
                    throw new ArgumentNullException("path");
                }

                Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                // binding data from the path template
                IReadOnlyDictionary<string, object> bindingDataFromPath = _bindingDataProvider.GetBindingData(path);
                if (bindingDataFromPath != null)
                {
                    foreach (KeyValuePair<string, object> item in bindingDataFromPath)
                    {
                        // In case of conflict, binding data from the path overrides
                        // the built-in binding data above.
                        bindingData[item.Key] = item.Value;
                    }
                }

                return bindingData;
            }

            public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
            {
                Task<IListener> listener = _parent._listenerBuilder(_attribute, context.Executor);
                return listener;
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor
                {
                    Name = _parameter.Name
                };
            }
        }
    }
}