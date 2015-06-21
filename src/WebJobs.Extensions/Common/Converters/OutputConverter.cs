using System.IO;
using Microsoft.Azure.WebJobs.Host.Converters;

namespace Microsoft.Azure.WebJobs.Extensions.Common.Converters
{
    internal class OutputConverter<TInput, TOutput> : IObjectToTypeConverter<TOutput> where TInput : class
    {
        private readonly IConverter<TInput, TOutput> _innerConverter;

        public OutputConverter(IConverter<TInput, TOutput> innerConverter)
        {
            _innerConverter = innerConverter;
        }

        public bool TryConvert(object input, out TOutput output)
        {
            TInput typedInput = input as TInput;

            if (typedInput == null)
            {
                output = default(TOutput);
                return false;
            }

            output = _innerConverter.Convert(typedInput);
            return true;
        }
    }
}
