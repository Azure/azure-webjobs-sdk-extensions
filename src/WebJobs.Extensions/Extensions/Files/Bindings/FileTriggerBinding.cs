// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Framework;
using Microsoft.Azure.WebJobs.Files.Listeners;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal class FileTriggerBinding : ITriggerBinding
    {
        private readonly ParameterInfo _parameter;
        private readonly FileTriggerAttribute _attribute;
        private readonly FilesConfiguration _config;
        private readonly IReadOnlyDictionary<string, Type> _bindingContract;
        private readonly BindingDataProvider _bindingDataProvider;
        private readonly TraceWriter _trace;

        public FileTriggerBinding(FilesConfiguration config, ParameterInfo parameter, TraceWriter trace)
        {
            _config = config;
            _parameter = parameter;
            _trace = trace;
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
                if (!string.IsNullOrEmpty(filePath))
                {
                    // TODO: This only supports Created events. For Dashboard invocation, how can we
                    // handle Change events?
                    string directory = Path.GetDirectoryName(filePath);
                    string fileName = Path.GetFileName(filePath);

                    fileEvent = new FileSystemEventArgs(WatcherChangeTypes.Created, directory, fileName);
                }
            }

            IValueBinder valueBinder = new FileValueBinder(_parameter, fileEvent);
            IReadOnlyDictionary<string, object> bindingData = GetBindingData(fileEvent);

            return Task.FromResult<ITriggerData>(new TriggerData(valueBinder, bindingData));
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            return Task.FromResult<IListener>(new FileListener(_config, _attribute, context.Executor, _trace));
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new FileTriggerParameterDescriptor
            {
                Name = _parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Prompt = "Enter a file path",
                    Description = string.Format("File event occurred on path '{0}'", _attribute.GetNormalizedPath()),
                    DefaultValue = Path.Combine(_config.RootPath, _attribute.Path)
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

        private class FileValueBinder : StreamValueBinder
        {
            private readonly ParameterInfo _parameter;
            private readonly FileSystemEventArgs _fileEvent;

            public FileValueBinder(ParameterInfo parameter, FileSystemEventArgs fileEvent)
                : base(parameter)
            {
                _parameter = parameter;
                _fileEvent = fileEvent;
            }

            public override object GetValue()
            {
                if (_parameter.ParameterType == typeof(FileSystemEventArgs))
                {
                    return _fileEvent;
                }
                else if (_parameter.ParameterType == typeof(FileInfo))
                {
                    return new FileInfo(_fileEvent.FullPath);
                }
                return base.GetValue();
            }

            protected override Stream GetStream()
            {
                return File.OpenRead(_fileEvent.FullPath);
            }

            public override string ToInvokeString()
            {
                return _fileEvent.FullPath;
            }
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
