// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.Core
{
    /// <summary>
    /// This provider provides a binding to Type <see cref="ExecutionContext"/>.
    /// </summary>
    internal class ExecutionContextBindingProvider : IBindingProvider
    {
        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (context.Parameter.ParameterType != typeof(ExecutionContext))
            {
                return Task.FromResult<IBinding>(null);
            }

            return Task.FromResult<IBinding>(new ExecutionContextBinding(context.Parameter));
        }

        private class ExecutionContextBinding : IBinding
        {
            private readonly ParameterInfo _parameter;

            public ExecutionContextBinding(ParameterInfo parameter)
            {
                _parameter = parameter;
            }

            public bool FromAttribute
            {
                get { return false; }
            }

            public Task<IValueProvider> BindAsync(BindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                return BindInternalAsync(new ExecutionContext
                {
                    InvocationId = context.FunctionInstanceId
                });
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                return BindInternalAsync(new ExecutionContext
                {
                    InvocationId = context.FunctionInstanceId
                });
            }

            private static Task<IValueProvider> BindInternalAsync(ExecutionContext executionContext)
            {
                return Task.FromResult<IValueProvider>(new ExecutionContextValueProvider(executionContext));
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor
                {
                    Name = _parameter.Name,
                    DisplayHints = new ParameterDisplayHints
                    {
                        Description = "Function ExecutionContext"
                    }
                };
            }

            private class ExecutionContextValueProvider : IValueProvider
            {
                private readonly ExecutionContext _context;

                public ExecutionContextValueProvider(ExecutionContext context)
                {
                    _context = context;
                }

                public Type Type
                {
                    get { return typeof(ExecutionContext); }
                }

                public object GetValue()
                {
                    return _context;
                }

                public string ToInvokeString()
                {
                    return _context.InvocationId.ToString();
                }
            }
        }
    }
}
