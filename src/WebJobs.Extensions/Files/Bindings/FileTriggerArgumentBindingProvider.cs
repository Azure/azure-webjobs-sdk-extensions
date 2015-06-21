using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Common.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal class FileTriggerArgumentBindingProvider<TInput, TOutput> : IArgumentBindingProvider<FileSystemEventArgs>
    {
        public IArgumentBinding<FileSystemEventArgs> TryCreate(ParameterInfo parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException("parameter");
            }

            if (parameter.ParameterType != typeof(TOutput))
            {
                return null;
            }

            return new ArgumentBinding();
        }

        private class ArgumentBinding : IArgumentBinding<FileSystemEventArgs>
        {
            public Type ValueType
            {
                get { return typeof(TOutput); }
            }

            public IReadOnlyDictionary<string, Type> BindingDataContract
            {
                get { return null; }
            }

            public Task<IValueProvider> BindAsync(FileSystemEventArgs value, ValueBindingContext context)
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                object converted = Convert(value);
                IValueProvider provider = new ValueProvider(converted, typeof(TOutput), value.FullPath);

                return Task.FromResult<IValueProvider>(provider);
            }

            private static TOutput Convert(FileSystemEventArgs input)
            {
                if (input == null)
                {
                    throw new System.ArgumentNullException("input");
                }

                object result = null;

                if (typeof(TOutput) == typeof(FileStream) ||
                    typeof(TOutput) == typeof(Stream))
                {
                    result = File.OpenRead(input.FullPath);
                }
                if (typeof(TOutput) == typeof(FileInfo))
                {
                    result = new FileInfo(input.FullPath);
                }
                else if (typeof(TOutput) == typeof(byte[]))
                {
                    result = File.ReadAllBytes(input.FullPath);
                }
                else if (typeof(TOutput) == typeof(string))
                {
                    result = File.ReadAllText(input.FullPath);
                }

                return (TOutput)result;
            }

            private class ValueProvider : IValueProvider
            {
                private readonly object _value;
                private readonly Type _valueType;
                private readonly string _invokeString;

                public ValueProvider(object value, Type valueType, string invokeString)
                {
                    if (value != null && !valueType.IsAssignableFrom(value.GetType()))
                    {
                        throw new InvalidOperationException("value is not of the correct type.");
                    }

                    _value = value;
                    _valueType = valueType;
                    _invokeString = invokeString;
                }

                public Type Type
                {
                    get { return _valueType; }
                }

                public object GetValue()
                {
                    return _value;
                }

                public string ToInvokeString()
                {
                    return _invokeString;
                }
            }
        }
    }
}
