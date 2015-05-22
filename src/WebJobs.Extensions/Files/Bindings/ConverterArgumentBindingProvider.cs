using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;

namespace WebJobs.Extensions.Files.Bindings
{
    internal class ConverterArgumentBindingProvider<T> : IFileTriggerArgumentBindingProvider
    {
        private readonly IAsyncConverter<FileSystemEventArgs, T> _converter;

        public ConverterArgumentBindingProvider(IAsyncConverter<FileSystemEventArgs, T> converter)
        {
            _converter = converter;
        }

        public IArgumentBinding<FileSystemEventArgs> TryCreate(ParameterInfo parameter)
        {
            if (parameter.ParameterType != typeof(T))
            {
                return null;
            }

            return new ConverterArgumentBinding(_converter);
        }

        internal class ConverterArgumentBinding : IArgumentBinding<FileSystemEventArgs>
        {
            private readonly IAsyncConverter<FileSystemEventArgs, T> _converter;

            public ConverterArgumentBinding(IAsyncConverter<FileSystemEventArgs, T> converter)
            {
                _converter = converter;
            }

            public Type ValueType
            {
                get { return typeof(T); }
            }

            public IReadOnlyDictionary<string, Type> BindingDataContract
            {
                get { return null; }
            }

            public async Task<IValueProvider> BindAsync(FileSystemEventArgs value, ValueBindingContext context)
            {
                // TODO: need to clone this?
                FileSystemEventArgs clone = new FileSystemEventArgs(value.ChangeType, Path.GetDirectoryName(value.FullPath), value.Name);

                object converted = await _converter.ConvertAsync(value, context.CancellationToken);
                IValueProvider provider = await FileSystemEventValueProvider.CreateAsync(clone, converted, typeof(T), context.CancellationToken);

                return provider;
            }
        }
    }
}
