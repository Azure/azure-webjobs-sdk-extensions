using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Fiels.Listeners;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace WebJobs.Extensions.Files.Bindings
{
    internal class FileTriggerBinding : ITriggerBinding<FileSystemEventArgs>
    {
        private readonly string _parameterName;
        private readonly IObjectToTypeConverter<FileSystemEventArgs> _converter;
        private readonly IArgumentBinding<FileSystemEventArgs> _argumentBinding;
        private readonly FileTriggerAttribute _attribute;
        private FilesConfiguration _config;
        private IReadOnlyDictionary<string, Type> _bindingContract;
        private BindingTemplateSource _bindingTemplateSource;

        public FileTriggerBinding(string parameterName, Type parameterType, IArgumentBinding<FileSystemEventArgs> argumentBinding, FilesConfiguration config, FileTriggerAttribute attribute)
        {
            _parameterName = parameterName;
            _converter = CreateConverter(parameterType);
            _argumentBinding = argumentBinding;
            _config = config;
            _attribute = attribute;
            _bindingContract = CreateBindingDataContract(attribute.Path);
        }

        public Type TriggerValueType
        {
            get
            {
                return typeof(FileSystemEventArgs);
            }
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _bindingContract; }
        }

        public async Task<ITriggerData> BindAsync(FileSystemEventArgs value, ValueBindingContext context)
        {
            IValueProvider valueProvider = await _argumentBinding.BindAsync(value, context);
            IReadOnlyDictionary<string, object> bindingData = CreateBindingData(value);

            return new TriggerData(valueProvider, bindingData);
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            FileSystemEventArgs message = null;

            if (!_converter.TryConvert(value, out message))
            {
                throw new InvalidOperationException("Unable to convert trigger to FileSystemEventArgs.");
            }

            return BindAsync(message, context);
        }

        public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor<FileSystemEventArgs> executor)
        {
            return new FileListenerFactory(_config, _attribute, executor);
        }

        public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor executor)
        {
            return new FileListenerFactory(_config, _attribute, (ITriggeredFunctionExecutor<FileSystemEventArgs>)executor);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new FileTriggerParameterDescriptor
            {
                Name = _parameterName,
                FilePath = _attribute.Path
                // TODO: Figure out Display Hints
            };
        }

        private IReadOnlyDictionary<string, Type> CreateBindingDataContract(string filePathPattern)
        {
            Dictionary<string, Type> contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            contract.Add("FileTrigger", typeof(string));

            _bindingTemplateSource = BindingTemplateSource.FromString(filePathPattern);
            Dictionary<string, Type> contractFromPath = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (string parameterName in _bindingTemplateSource.ParameterNames)
            {
                contract.Add(parameterName, typeof(string));
            }

            if (contractFromPath != null)
            {
                foreach (KeyValuePair<string, Type> item in contractFromPath)
                {
                    // In case of conflict, binding data from the value type overrides the built-in binding data above.
                    contract[item.Key] = item.Value;
                }
            }

            return contract;
        }

        private IReadOnlyDictionary<string, object> CreateBindingData(FileSystemEventArgs fileEvent)
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("FileTrigger", fileEvent.FullPath);

            string pathRoot = Path.GetDirectoryName(_attribute.Path);
            int idx = fileEvent.FullPath.IndexOf(pathRoot);
            string pathToMatch = fileEvent.FullPath.Substring(idx);
            IReadOnlyDictionary<string, object> bindingDataFromPath = _bindingTemplateSource.CreateBindingData(pathToMatch);

            if (bindingDataFromPath != null)
            {
                foreach (KeyValuePair<string, object> item in bindingDataFromPath)
                {
                    // In case of conflict, binding data from the value type overrides the built-in binding data above.
                    bindingData[item.Key] = item.Value;
                }
            }
            return bindingData;
        }

        private static IObjectToTypeConverter<FileSystemEventArgs> CreateConverter(Type parameterType)
        {
            return new CompositeObjectToTypeConverter<FileSystemEventArgs>(
                    new OutputConverter<FileSystemEventArgs>(new IdentityConverter<FileSystemEventArgs>()));
        }
    }
}
