using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Common.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    /// <summary>
    /// Argument binding provider for the out parameter types supported by the File binding.
    /// </summary>
    /// <typeparam name="TArgument">The out parameter type.</typeparam>
    internal class FileOutputArgumentBindingProvider<TArgument> : IArgumentBindingProvider<FileBindingInfo>
    {
        public IArgumentBinding<FileBindingInfo> TryCreate(ParameterInfo parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException("parameter");
            }

            if (!parameter.IsOut || parameter.ParameterType != typeof(TArgument).MakeByRefType())
            {
                return null;
            }

            return new ArgumentBinding();
        }

        private class ArgumentBinding : IArgumentBinding<FileBindingInfo>
        {
            public Type ValueType
            {
                get { return typeof(TArgument); }
            }

            public Task<IValueProvider> BindAsync(FileBindingInfo value, ValueBindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                IValueProvider provider = new ValueBinder(value);

                return Task.FromResult(provider);
            }

            internal class ValueBinder : IOrderedValueBinder
            {
                private readonly FileBindingInfo _bindingInfo;

                public ValueBinder(FileBindingInfo bindingInfo)
                {
                    _bindingInfo = bindingInfo;
                }

                public BindStepOrder StepOrder
                {
                    get { return BindStepOrder.Enqueue; }
                }

                public Type Type
                {
                    get { return typeof(TArgument); }
                }

                public object GetValue()
                {
                    // out parameters don't have an initial value before the function
                    // is invoked
                    return null;
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

                    // convert the value as needed into a byte[]
                    byte[] bytes = null;
                    if (value.GetType() == typeof(string))
                    {
                        bytes = Encoding.UTF8.GetBytes((string)value);
                    }
                    else if (value.GetType() == typeof(byte[]))
                    {
                        bytes = (byte[])value;
                    }

                    // open the file using the declared file options, and write the bytes
                    using (FileStream fileStream = _bindingInfo.FileInfo.Open(_bindingInfo.Attribute.Mode, _bindingInfo.Attribute.Access))
                    {
                        fileStream.Write(bytes, 0, bytes.Length);
                    }

                    return Task.FromResult(true);
                }
            }
        }
    }
}
