using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace WebJobs.Extensions.Files.Bindings
{
    internal class FileSystemEventValueProvider : IValueProvider
    {
        private readonly object _value;
        private readonly Type _valueType;
        private readonly string _invokeString;

        private FileSystemEventValueProvider(object value, Type valueType, string invokeString)
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

        public static async Task<FileSystemEventValueProvider> CreateAsync(FileSystemEventArgs clone, object value, Type valueType, CancellationToken cancellationToken)
        {
            string invokeString = await CreateInvokeStringAsync(clone, cancellationToken);
            return new FileSystemEventValueProvider(value, valueType, invokeString);
        }

        private static Task<string> CreateInvokeStringAsync(FileSystemEventArgs clone, CancellationToken cancellationToken)
        {
            return Task.FromResult<string>(clone.FullPath);
        }
    }
}
