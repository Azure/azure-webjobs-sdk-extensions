// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    /// <summary>
    /// Provides an <see cref="IBinding"/> for valid input item parameters decorated with
    /// an <see cref="DocumentDBAttribute"/>. The attribute must contain a non-null <see cref="DocumentDBAttribute.Id"/> value
    /// that will be used to lookup the document and populate the method parameter.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="JObject"/></description></item>
    /// <item><description><see cref="Document"/></description></item>
    /// </list>
    /// </remarks>
    internal class DocumentDBItemBinding : IBinding, IBindingProvider
    {
        private ParameterInfo _parameter;
        private DocumentDBContext _context;
        private BindingTemplate _bindingTemplate;

        public DocumentDBItemBinding(ParameterInfo parameter, DocumentDBContext context, BindingProviderContext bindingContext)
        {
            _parameter = parameter;
            _context = context;

            // set up binding for '{ItemId}'-type bindings
            if (_context.ResolvedId != null)
            {
                _bindingTemplate = BindingTemplate.FromString(_context.ResolvedId);
                _bindingTemplate.ValidateContractCompatibility(bindingContext.BindingDataContract);
            }
        }

        public bool FromAttribute
        {
            get { return true; }
        }

        public Task<IValueProvider> BindAsync(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            string id = ResolveId(context.BindingData);
            return BindAsync(id, context.ValueContext);
        }

        public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            Type paramType = _parameter.ParameterType;
            return Task.FromResult(CreateItemValueProvider(paramType, value as string));
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
            // This will be the last IBindingProvider checked in the CompositeBindingProvider,
            // so simply return. DocumentDB accepts any object as a parameter so we'll allow
            // everything to flow through. DocumentClient will tell us later if there's an issue.

            return Task.FromResult<IBinding>(this);
        }

        internal string ResolveId(IReadOnlyDictionary<string, object> bindingData)
        {
            string id = null;
            if (_bindingTemplate != null)
            {
                id = _bindingTemplate.Bind(bindingData);
            }
            return id;
        }

        private IValueProvider CreateItemValueProvider(Type coreType, string id)
        {
            Type genericType = typeof(DocumentDBItemValueBinder<>).MakeGenericType(coreType);
            return (IValueProvider)Activator.CreateInstance(genericType, _parameter, _context, id);
        }
    }
}