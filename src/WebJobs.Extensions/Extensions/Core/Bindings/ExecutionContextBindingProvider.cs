// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using static Microsoft.Azure.WebJobs.CoreJobHostConfigurationExtensions;

namespace Microsoft.Azure.WebJobs.Extensions.Core
{
    /// <summary>
    /// This provider provides a binding to Type <see cref="ExecutionContext"/>.
    /// </summary>
    internal class ExecutionContextBindingProvider : IBindingProvider
    {
        private readonly CoreExtensionConfig _config;

        public ExecutionContextBindingProvider(CoreExtensionConfig config)
        {
            _config = config;
        }

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

            return Task.FromResult<IBinding>(new ExecutionContextBinding(context.Parameter, _config));
        }

        private class ExecutionContextBinding : IBinding
        {
            private readonly ParameterInfo _parameter;
            private readonly CoreExtensionConfig _config;

            public ExecutionContextBinding(ParameterInfo parameter, CoreExtensionConfig config)
            {
                _parameter = parameter;
                _config = config;
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

                return BindInternalAsync(CreateContext(context.ValueContext));
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                return BindInternalAsync(CreateContext(context));
            }

            private ExecutionContext CreateContext(ValueBindingContext context)
            {
                var result = new ExecutionContext
                {
                    InvocationId = context.FunctionInstanceId,
                    FunctionName = context.FunctionContext.MethodName,
                    FunctionDirectory = Environment.CurrentDirectory,
                    FunctionAppDirectory = _config.AppDirectory
                };

                if (result.FunctionAppDirectory != null)
                {
                    result.FunctionDirectory = Path.Combine(result.FunctionAppDirectory, result.FunctionName);
                }
                return result;
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

                public Task<object> GetValueAsync()
                {
                    return Task.FromResult<object>(_context);
                }

                public string ToInvokeString()
                {
                    return _context.InvocationId.ToString();
                }
            }
        }
    }
}
