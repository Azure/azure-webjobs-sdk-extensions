using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Common.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    /// <summary>
    /// Argument binding provider that binds Stream and FileStream parameters
    /// </summary>
    internal class FileStreamArgumentBindingProvider : IArgumentBindingProvider<FileBindingInfo>
    {
        public IArgumentBinding<FileBindingInfo> TryCreate(ParameterInfo parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException("parameter");
            }

            if (parameter.ParameterType != typeof(FileStream) &&
                parameter.ParameterType != typeof(Stream))
            {
                return null;
            }

            return new FileStreamArgumentBinding(parameter.ParameterType);
        }

        private class FileStreamArgumentBinding : IArgumentBinding<FileBindingInfo>
        {
            private Type _streamType;

            public FileStreamArgumentBinding(Type streamType)
            {
                _streamType = streamType;
            }

            public Type ValueType
            {
                get { return _streamType; }
            }

            public Task<IValueProvider> BindAsync(FileBindingInfo value, ValueBindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                IValueProvider provider = new FileStreamValueBinder(_streamType, value);

                return Task.FromResult(provider);
            }

            private class FileStreamValueBinder : IOrderedValueBinder
            {
                private readonly FileBindingInfo _bindingInfo;
                private Type _streamType;

                public FileStreamValueBinder(Type streamType, FileBindingInfo bindingInfo)
                {
                    _streamType = streamType;
                    _bindingInfo = bindingInfo;
                }

                public BindStepOrder StepOrder
                {
                    get { return BindStepOrder.Enqueue; }
                }

                public Type Type
                {
                    get { return _streamType; }
                }

                public object GetValue()
                {
                    // Right before the job function is invoked, we get called to open and return
                    // the file stream
                    return _bindingInfo.FileInfo.Open(_bindingInfo.Attribute.Mode, _bindingInfo.Attribute.Access);
                }

                public string ToInvokeString()
                {
                    return _bindingInfo.FileInfo.FullName;
                }

                public Task SetValueAsync(object value, CancellationToken cancellationToken)
                {
                    if (value == null)
                    {
                        return Task.FromResult(0);
                    }

                    // after the job function is finished, we get called again allowing
                    // us to flush and close the file
                    FileStream fileStream = (FileStream)value;
                    fileStream.Close();

                    return Task.FromResult(true);
                }
            }
        }
    }
}
