// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Files.Listener;
using Microsoft.Azure.WebJobs.Files.Listeners;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal class FileTriggerBinding : ITriggerBinding
    {
        private readonly ParameterInfo _parameter;
        private readonly FileTriggerAttribute _attribute;
        private readonly IOptions<FilesOptions> _options;
        private readonly IReadOnlyDictionary<string, Type> _bindingContract;
        private readonly BindingDataProvider _bindingDataProvider;
        private readonly IFileProcessorFactory _fileProcessorFactory;
        private readonly ILogger _logger;

        public FileTriggerBinding(IOptions<FilesOptions> options, ParameterInfo parameter, ILogger logger, IFileProcessorFactory fileProcessorFactory)
        {
            _options = options;
            _parameter = parameter;
            _logger = logger;
            _fileProcessorFactory = fileProcessorFactory;
            _attribute = parameter.GetCustomAttribute<FileTriggerAttribute>(inherit: false);
            _bindingDataProvider = BindingDataProvider.FromTemplate(_attribute.Path);
            _bindingContract = CreateBindingContract();
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
            get { return typeof(FileSystemEventArgs); }
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            FileSystemEventArgs fileEvent = value as FileSystemEventArgs;
            if (fileEvent == null)
            {
                string filePath = value as string;
                fileEvent = GetFileArgsFromString(filePath);
            }

            IReadOnlyDictionary<string, object> bindingData = GetBindingData(fileEvent);

            return Task.FromResult<ITriggerData>(new TriggerData(null, bindingData));
        }

        internal static FileSystemEventArgs GetFileArgsFromString(string filePath)
        {            
            if (!string.IsNullOrEmpty(filePath))
            {
                // TODO: This only supports Created events. For Dashboard invocation, how can we
                // handle Change events?
                string directory = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);

                return new FileSystemEventArgs(WatcherChangeTypes.Created, directory, fileName);
            }

            return null;
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            return Task.FromResult<IListener>(new FileListener(_options, _attribute, context.Executor, _logger, _fileProcessorFactory));
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            // These path values are validated later during startup.
            string triggerPath = Path.Combine(_options.Value.RootPath ?? string.Empty, _attribute.Path ?? string.Empty);

            return new FileTriggerParameterDescriptor
            {
                Name = _parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Prompt = "Enter a file path",
                    Description = string.Format("File event occurred on path '{0}'", _attribute.GetRootPath()),
                    DefaultValue = triggerPath
                }
            };
        }

        private IReadOnlyDictionary<string, Type> CreateBindingContract()
        {
            Dictionary<string, Type> contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            contract.Add("FileTrigger", typeof(FileSystemEventArgs));

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

        private IReadOnlyDictionary<string, object> GetBindingData(FileSystemEventArgs value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            // built in binding data
            Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("FileTrigger", value);

            string pathRoot = Path.GetDirectoryName(_attribute.Path);
            int idx = value.FullPath.IndexOf(pathRoot, StringComparison.OrdinalIgnoreCase);
            string pathToMatch = value.FullPath.Substring(idx);

            // binding data from the path template
            IReadOnlyDictionary<string, object> bindingDataFromPath = _bindingDataProvider.GetBindingData(pathToMatch);
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

        private class FileTriggerParameterDescriptor : TriggerParameterDescriptor
        {
            public override string GetTriggerReason(IDictionary<string, string> arguments)
            {
                string fullPath = null;
                if (arguments != null && arguments.TryGetValue(Name, out fullPath))
                {
                    return string.Format("File change detected for file '{0}'", fullPath);
                }
                return null;
            }
        }
    }
}
