using Microsoft.Azure.WebJobs.Host.Bindings;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    internal class ConstantObj : IValueBinder
    {
        internal object _value;
        internal Func<object, Task> _onCompleted;

        public Type Type { get; set; }

        public object GetValue()
        {
            if ((Type == typeof(byte[]) || Type == typeof(byte[]).MakeByRefType()) && _value is MemoryStream)
            {
                return ((MemoryStream)_value).ToArray();
            }
            else
            {
                return _value;
            }
        }

        public Task SetValueAsync(object value, CancellationToken cancellationToken)
        {
            if (value == null)
            {
                return Task.FromResult(0);
            }

            if (_onCompleted != null)
            {
                return _onCompleted(value); // Flush hook 
            }

            if (typeof(Stream).IsAssignableFrom(value.GetType()))
            {
                Stream stream = (Stream)value;
                stream.Close();
            }
            else if (typeof(TextWriter).IsAssignableFrom(value.GetType()))
            {
                TextWriter writer = (TextWriter)value;
                writer.Close();
            }
            else if (typeof(TextReader).IsAssignableFrom(value.GetType()))
            {
                TextReader reader = (TextReader)value;
                reader.Close();
            }

            return Task.FromResult(0);
        }

        public string ToInvokeString()
        {
            return string.Empty;
        }
    }
}