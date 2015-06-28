using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Framework;
using Microsoft.Azure.WebJobs.Files.Listeners;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
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
        private readonly BindingContract _bindingContract;

        public FileTriggerBinding(FilesConfiguration config, ParameterInfo parameter)
        {
            _config = config;
            _parameter = parameter;
            _attribute = parameter.GetCustomAttribute<FileTriggerAttribute>(inherit: false);
            _bindingContract = CreateBindingContract();
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get
            {
                return _bindingContract.BindingDataContract;
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

        public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor executor)
        {
            return new FileListenerFactory(_config, _attribute, executor);
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

        private BindingContract CreateBindingContract()
        {
            Dictionary<string, Type> contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            contract.Add("FileTrigger", typeof(FileSystemEventArgs));
            return new BindingContract(_attribute.Path, contract);
        }

        private IReadOnlyDictionary<string, object> GetBindingData(FileSystemEventArgs value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("FileTrigger", value);

            string pathRoot = Path.GetDirectoryName(_attribute.Path);
            int idx = value.FullPath.IndexOf(pathRoot, StringComparison.OrdinalIgnoreCase);
            string pathToMatch = value.FullPath.Substring(idx);

            return _bindingContract.GetBindingData(pathToMatch, bindingData);
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
