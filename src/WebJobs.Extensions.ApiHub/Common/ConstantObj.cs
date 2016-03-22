using Microsoft.Azure.WebJobs.Host.Bindings;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    class ConstantObj : IValueBinder
    {
        internal object _value;
        internal Func<Task> _onCompleted;

        public Type Type { get; set; }

        public object GetValue()
        {
            return _value;
        }

        public Task SetValueAsync(object value, CancellationToken cancellationToken)
        {
            if (_onCompleted != null)
            {
                return _onCompleted(); // Flush hook 
            }
            return Task.FromResult(0);
        }

        public string ToInvokeString()
        {
            return "Na";
        }
    }
}