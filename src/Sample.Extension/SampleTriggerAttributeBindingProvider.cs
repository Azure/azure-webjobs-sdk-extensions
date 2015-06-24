using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Sample.Extension
{
    public class SampleTriggerValue
    {
        // TODO: Define the default type that your trigger binding
        // binds to.
    }

    internal class SampleTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private SampleConfiguration _config;

        public SampleTriggerAttributeBindingProvider(SampleConfiguration config)
        {
            _config = config;
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            SampleTriggerAttribute attribute = parameter.GetCustomAttribute<SampleTriggerAttribute>(inherit: false);

            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            // We know our attribute is applied to this parameter. Ensure that the parameter
            // Type is one we support
            if (!Binding.CanBind(parameter))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Can't bind SampleTrigger to type '{0}'.", parameter.ParameterType));
            }

            return Task.FromResult<ITriggerBinding>(new Binding(parameter, attribute));
        }

        private class Binding : ITriggerBinding<SampleTriggerValue>
        {
            private ParameterInfo _parameter;
            private SampleTriggerAttribute _attribute;
            private IReadOnlyDictionary<string, Type> _bindingContract;

            public Binding(ParameterInfo parameter, SampleTriggerAttribute attribute)
            {
                _parameter = parameter;
                _attribute = attribute;
                _bindingContract = CreateBindingDataContract();
            }

            public Type TriggerValueType
            {
                get { return typeof(SampleTriggerValue); }
            }

            public IReadOnlyDictionary<string, Type> BindingDataContract
            {
                get { return _bindingContract; }
            }

            public static bool CanBind(ParameterInfo parameter)
            {
                // TODO: Define the types your binding supports here
                return parameter.ParameterType == typeof(SampleTriggerValue) ||
                       parameter.ParameterType == typeof(string);
            }

            public Task<ITriggerData> BindAsync(SampleTriggerValue value, ValueBindingContext context)
            {
                // TODO: perform any required conversions from the trigger value
                // to the parameter type
                object converted = value;
                if (_parameter.ParameterType == typeof(string))
                {
                    converted = value.ToString();
                }

                IValueProvider valueProvider = new ValueProvider(converted, _parameter.ParameterType);
                IReadOnlyDictionary<string, object> bindingData = CreateBindingData(value);

                return Task.FromResult<ITriggerData>(new TriggerData(valueProvider, bindingData));
            }

            public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                // TODO: convert the incoming value as needed into your
                // trigger value type. For example, this may be a string
                // value coming from a Dashboard Run/Replay invocation
                SampleTriggerValue triggerValue = new SampleTriggerValue();
                return BindAsync(triggerValue, context);
            }

            public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor<SampleTriggerValue> executor)
            {
                return new ListenerFactory(executor);
            }

            public IListenerFactory CreateListenerFactory(FunctionDescriptor descriptor, ITriggeredFunctionExecutor executor)
            {
                return CreateListenerFactory(descriptor, (ITriggeredFunctionExecutor<SampleTriggerValue>)executor);
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new SampleTriggerParameterDescriptor
                {
                    Name = _parameter.Name,
                    DisplayHints = new ParameterDisplayHints
                    {
                        // TODO: Customize your Dashboard display strings
                        Prompt = "Sample",
                        Description = "Sample trigger fired",
                        DefaultValue = "Sample"
                    }
                };
            }

            private IReadOnlyDictionary<string, Type> CreateBindingDataContract()
            {
                Dictionary<string, Type> contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                contract.Add("SampleTrigger", typeof(SampleTriggerValue));

                // TODO: Add any additional binding contract members

                return contract;
            }

            private IReadOnlyDictionary<string, object> CreateBindingData(SampleTriggerValue value)
            {
                Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                bindingData.Add("SampleTrigger", value);

                // TODO: Add any additional binding data

                return bindingData;
            }

            internal class SampleTriggerParameterDescriptor : TriggerParameterDescriptor
            {
                public override string GetTriggerReason(IDictionary<string, string> arguments)
                {
                    // TODO: Customize your Dashboard display string
                    return string.Format("Sample trigger fired at {0}", DateTime.UtcNow.ToString("o"));
                }
            }

            private class ListenerFactory : IListenerFactory
            {
                private ITriggeredFunctionExecutor<SampleTriggerValue> _executor;

                public ListenerFactory(ITriggeredFunctionExecutor<SampleTriggerValue> executor)
                {
                    _executor = executor;
                }

                public Task<IListener> CreateAsync(ListenerFactoryContext context)
                {
                    return Task.FromResult<IListener>(new Listener(_executor));
                }

                private class Listener : IListener
                {
                    private ITriggeredFunctionExecutor<SampleTriggerValue> _executor;
                    private System.Timers.Timer _timer;

                    public Listener(ITriggeredFunctionExecutor<SampleTriggerValue> executor)
                    {
                        _executor = executor;

                        // TODO: For this sample, we're using a timer to generate
                        // trigger events. You'll replace this with your event source.
                        _timer = new System.Timers.Timer(5 * 1000)
                        {
                            AutoReset = true
                        };
                        _timer.Elapsed += OnTimer;
                    }

                    public Task StartAsync(CancellationToken cancellationToken)
                    {
                        // TODO: Start monitoring your event source
                        _timer.Start();
                        return Task.FromResult(true);
                    }

                    public Task StopAsync(CancellationToken cancellationToken)
                    {
                        // TODO: Stop monitoring your event source
                        _timer.Stop();
                        return Task.FromResult(true);
                    }

                    public void Dispose()
                    {
                        // TODO: Perform any final cleanup
                        _timer.Dispose();
                    }

                    public void Cancel()
                    {
                        // TODO: cancel any outstanding tasks initiated by this listener
                    }

                    private void OnTimer(object sender, System.Timers.ElapsedEventArgs e)
                    {
                        // TODO: When you receive new events from your event source,
                        // invoke the function executor
                        TriggeredFunctionData<SampleTriggerValue> input = new TriggeredFunctionData<SampleTriggerValue>
                        {
                            TriggerValue = new SampleTriggerValue()
                        };
                        _executor.TryExecuteAsync(input, CancellationToken.None).Wait();
                    }
                }
            }

            private class ValueProvider : IValueProvider
            {
                private object _value;

                public ValueProvider(object value, Type type)
                {
                    _value = value;
                    Type = type;
                }

                public object GetValue()
                {
                    return _value;
                }

                public string ToInvokeString()
                {
                    // TODO: Customize your Dashboard invoke string
                    return "Sample";
                }

                public Type Type { get; private set; }
            }
        }
    }
}
