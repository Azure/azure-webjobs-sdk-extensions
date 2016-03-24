// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBClientBinding : IBinding, IBindingProvider
    {
        private ParameterInfo _parameter;
        private DocumentDBContext _context;

        public DocumentDBClientBinding(ParameterInfo parameter, DocumentDBContext context)
        {
            _parameter = parameter;
            _context = context;
        }

        public bool FromAttribute
        {
            get
            {
                return true;
            }
        }

        public Task<IValueProvider> BindAsync(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            return BindAsync(null, context.ValueContext);
        }

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            return Task.FromResult<IValueProvider>(new DocumentDBClientValueProvider(_context));
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor
            {
                Name = _parameter.Name
            };
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (context.Parameter.ParameterType == typeof(DocumentClient))
            {
                return Task.FromResult<IBinding>(this);
            }

            return Task.FromResult<IBinding>(null);
        }
    }
}
