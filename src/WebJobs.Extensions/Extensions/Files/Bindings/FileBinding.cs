﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal class FileBinding : IBinding
    {
        private readonly IOptions<FilesOptions> _options;
        private readonly ParameterInfo _parameter;
        private readonly BindingTemplate _bindingTemplate;
        private readonly FileAttribute _attribute;

        public FileBinding(IOptions<FilesOptions> options, ParameterInfo parameter, BindingTemplate bindingTemplate)
        {
            _options = options;
            _parameter = parameter;
            _bindingTemplate = bindingTemplate;
            _attribute = _parameter.GetCustomAttribute<FileAttribute>(inherit: false);
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

            string boundFileName = _bindingTemplate.Bind(context.BindingData);
            string filePath = Path.Combine(_options.Value.RootPath, boundFileName);
            FileInfo fileInfo = new FileInfo(filePath);

            return BindAsync(fileInfo, context.ValueContext);
        }

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            FileInfo fileInfo = value as FileInfo;
            if (fileInfo == null && value.GetType() == typeof(string))
            {
                fileInfo = new FileInfo((string)value);
            }

            return Task.FromResult<IValueProvider>(new FileValueBinder(_parameter, _attribute, fileInfo));
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor
            {
                Name = _parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Prompt = "Enter a file path",
                    Description = string.Format("{0} file {1}", _attribute.Access.ToString(), _attribute.Path),
                    DefaultValue = Path.Combine(_options.Value.RootPath, _attribute.Path)
                }
            };
        }

        private class FileValueBinder : StreamValueBinder
        {
            private readonly ParameterInfo _parameter;
            private readonly FileAttribute _attribute;
            private readonly FileInfo _fileInfo;

            public FileValueBinder(ParameterInfo parameter, FileAttribute attribute, FileInfo fileInfo)
                : base(parameter)
            {
                _parameter = parameter;
                _attribute = attribute;
                _fileInfo = fileInfo;
            }

            protected override Stream GetStream()
            {
                return _fileInfo.Open(_attribute.Mode, _attribute.Access);
            }

            public override Task<object> GetValueAsync()
            {
                if (_parameter.ParameterType == typeof(FileInfo))
                {
                    return Task.FromResult<object>(_fileInfo);
                }
                return base.GetValueAsync();
            }

            public override string ToInvokeString()
            {
                return _fileInfo.FullName;
            }
        }
    }
}