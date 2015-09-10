using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Framework;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    internal class WebHookTriggerBinding : ITriggerBinding
    {
        private readonly WebHookDispatcher _dispatcher;
        private readonly ParameterInfo _parameter;
        private readonly IReadOnlyDictionary<string, Type> _bindingContract;
        private Uri _route;

        public WebHookTriggerBinding(WebHookDispatcher dispatcher, ParameterInfo parameter, WebHookTriggerAttribute attribute)
        {
            _dispatcher = dispatcher;
            _parameter = parameter;
            _bindingContract = CreateBindingDataContract();

            Uri baseAddress = new Uri(string.Format("http://localhost:{0}", dispatcher.Port));
            _route = FormatWebHookUri(baseAddress, attribute, parameter);
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _bindingContract; }
        }

        public Type TriggerValueType
        {
            get { return typeof(HttpRequestMessage); }
        }

        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            HttpRequestMessage request = value as HttpRequestMessage;
            if (request == null && value != null && value.GetType() == typeof(string))
            {
                request = WebHookValueBinder.FromInvokeString((string)value);
            }

            IValueBinder valueBinder = new WebHookValueBinder(_parameter, request);
            return Task.FromResult<ITriggerData>(new TriggerData(valueBinder, GetBindingData(request)));
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            WebHookListener listener = new WebHookListener(_dispatcher, (MethodInfo)_parameter.Member, _route, context.Executor);
            return Task.FromResult<IListener>(listener);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new WebHookTriggerParameterDescriptor
            {
                Name = _parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    Prompt = "Enter request info",
                    DefaultValue = WebHookValueBinder.ToInvokeString(new HttpRequestMessage(HttpMethod.Post, _route))
                }
            };
        }

        internal static Uri FormatWebHookUri(Uri baseAddress, WebHookTriggerAttribute attribute, ParameterInfo parameter)
        {
            // build the full route from the base route
            string subRoute;
            if (!string.IsNullOrEmpty(attribute.Route))
            {
                subRoute = attribute.Route;
            }
            else
            {
                MethodInfo method = (MethodInfo)parameter.Member;
                subRoute = string.Format("{0}/{1}", method.DeclaringType.Name, method.Name);
            }
            return new Uri(baseAddress, subRoute);
        }

        private IReadOnlyDictionary<string, object> GetBindingData(HttpRequestMessage value)
        {
            Dictionary<string, object> bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bindingData.Add("WebHookTrigger", value);

            // TODO: Add any additional binding data

            return bindingData;
        }

        private IReadOnlyDictionary<string, Type> CreateBindingDataContract()
        {
            Dictionary<string, Type> contract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            contract.Add("WebHookTrigger", typeof(HttpRequestMessage));

            return contract;
        }

        private class WebHookTriggerParameterDescriptor : TriggerParameterDescriptor
        {
            public override string GetTriggerReason(IDictionary<string, string> arguments)
            {
                string invokeString = null;
                if (arguments != null && arguments.TryGetValue(Name, out invokeString))
                {
                    HttpRequestMessage request = WebHookValueBinder.FromInvokeString(invokeString);
                    return string.Format("WebHook triggered by request to '{0}'", request.RequestUri);
                }
                return null;
            }
        }

        private class WebHookValueBinder : StreamValueBinder
        {
            private readonly ParameterInfo _parameter;
            private readonly HttpRequestMessage _request;

            public WebHookValueBinder(ParameterInfo parameter, HttpRequestMessage request)
                : base(parameter)
            {
                _parameter = parameter;
                _request = request;
            }

            public override object GetValue()
            {
                if (_parameter.ParameterType == typeof(HttpRequestMessage))
                {
                    return _request;
                }
                return base.GetValue();
            }

            protected override Stream GetStream()
            {
                Task<Stream> task = _request.Content.ReadAsStreamAsync();
                task.Wait();
                return task.Result;
            }

            public override string ToInvokeString()
            {
                return ToInvokeString(_request);
            }

            internal static string ToInvokeString(HttpRequestMessage request)
            {
                string body = string.Empty;
                if (request.Content != null)
                {
                    Task<string> task = request.Content.ReadAsStringAsync();
                    task.Wait();
                    body = task.Result;
                }

                JObject jObject = new JObject()
                {
                    { "url", request.RequestUri.ToString() },
                    { "body", body }
                };

                string invokeString = jObject.ToString();
                return invokeString;
            }

            internal static HttpRequestMessage FromInvokeString(string value)
            {
                JObject jObject = JObject.Parse((string)value);
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, (string)jObject["url"]);
                request.Content = new StringContent((string)jObject["body"]);

                return request;
            }
        }
    }
}
