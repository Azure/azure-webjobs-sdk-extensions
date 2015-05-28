using System.IO;
using Microsoft.Azure.WebJobs.Host.Converters;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal class OutputConverter<TInput> : IObjectToTypeConverter<FileSystemEventArgs> where TInput : class
    {
        private readonly IConverter<TInput, FileSystemEventArgs> _innerConverter;

        public OutputConverter(IConverter<TInput, FileSystemEventArgs> innerConverter)
        {
            _innerConverter = innerConverter;
        }

        public bool TryConvert(object input, out FileSystemEventArgs output)
        {
            TInput typedInput = input as TInput;

            if (typedInput == null)
            {
                output = null;
                return false;
            }

            output = _innerConverter.Convert(typedInput);
            return true;
        }
    }
}
