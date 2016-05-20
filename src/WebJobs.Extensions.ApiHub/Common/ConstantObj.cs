// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub.Common
{
    internal class ConstantObj : IValueBinder
    {
        public Type Type { get; set; }

        public object Value { get; set; }

        public Func<object, Task> OnCompleted { get; set; }

        public object GetValue()
        {
            if ((Type == typeof(byte[]) || Type == typeof(byte[]).MakeByRefType()) && Value is MemoryStream)
            {
                return ((MemoryStream)Value).ToArray();
            }
            else
            {
                return Value;
            }
        }

        public Task SetValueAsync(object value, CancellationToken cancellationToken)
        {
            if (OnCompleted != null)
            {
                return OnCompleted(value); // Flush hook 
            }

            if (value == null)
            {
                return Task.FromResult(0);
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