using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Sample.Extension
{
    internal class SampleAttributeBindingProvider : IBindingProvider
    {
        private SampleConfiguration _config;

        public SampleAttributeBindingProvider(SampleConfiguration config)
        {
            _config = config;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            // Determine whether we should bind to the current parameter
            ParameterInfo parameter = context.Parameter;
            SampleAttribute attribute = parameter.GetCustomAttribute<SampleAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            // We know our attribute is applied to this parameter. Ensure that the parameter
            // Type is one we support
            if (!Binding.ValueBinder.CanBind(parameter))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Can't bind Sample to type '{0}'.", parameter.ParameterType));
            }

            return Task.FromResult<IBinding>(new Binding(parameter, attribute));
        }

        internal class Binding : IBinding
        {
            private ParameterInfo _parameter;
            private SampleAttribute _attribute;

            public Binding(ParameterInfo parameter, SampleAttribute attribute)
            {
                _parameter = parameter;
                _attribute = attribute;
            }

            public Task<IValueProvider> BindAsync(BindingContext context)
            {
                return Task.FromResult<IValueProvider>(new ValueBinder(_parameter));
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                // TODO: convert the incoming value as needed
                // before binding. For example, this may be a string
                // value coming from a Dashboard Run/Replay invocation
                return Task.FromResult<IValueProvider>(new ValueBinder(_parameter));
            }

            public bool FromAttribute
            {
                get { return true; }
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor
                {
                    Name = _parameter.Name,
                    DisplayHints = new ParameterDisplayHints
                    {
                        // TODO: Define your Dashboard integration strings here.
                        Description = "Sample",
                        DefaultValue = "Sample",
                        Prompt = "Please enter a Sample value"
                    }
                };
            }

            internal class ValueBinder : IOrderedValueBinder
            {
                private ParameterInfo _parameter;

                public ValueBinder(ParameterInfo parameter)
                {
                    _parameter = parameter;
                }

                public BindStepOrder StepOrder
                {
                    get { return BindStepOrder.Enqueue; }
                }

                public Type Type
                {
                    get { return _parameter.ParameterType; }
                }

                public static bool CanBind(ParameterInfo parameter)
                {
                    // TODO: Define the types your binding supports here and below
                    return parameter.ParameterType == typeof(Stream) ||
                            (parameter.IsOut && parameter.ParameterType == typeof(string).MakeByRefType());
                }

                public object GetValue()
                {           
                    if (_parameter.IsOut)
                    {
                        return null;
                    }

                    // TODO: Generate the parameter value that will be passed to
                    // the function.
                    object value = null;
                    if (_parameter.ParameterType == typeof(Stream))
                    {
                        value = new MemoryStream();
                    }
                    
                    return value;
                }

                public string ToInvokeString()
                {
                    // TODO: Return the string that should be shown in the Dashboard
                    // for this parameter
                    return "Sample";
                }

                public Task SetValueAsync(object value, CancellationToken cancellationToken)
                {
                    if (value == null)
                    {
                        return Task.FromResult(0);
                    }

                    // TODO: Process the final value as needed. For example, persist the value
                    // to an external service, etc.
                    if (_parameter.IsOut)
                    {
                        if (_parameter.ParameterType == typeof(string).MakeByRefType())
                        {
                            string stringValue = (string)value;
                        }
                    }
                    else
                    {
                        if (_parameter.ParameterType == typeof(Stream))
                        {
                            Stream stream = (Stream)value;
                            stream.Flush();
                            stream.Close();
                        }
                    }

                    return Task.FromResult(true);
                }
            }
        }
    }
}
