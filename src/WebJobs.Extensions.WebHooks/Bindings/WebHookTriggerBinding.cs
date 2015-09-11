// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    internal class WebHookTriggerBinding : ITriggerBinding
    {
        private readonly WebHookDispatcher _dispatcher;
        private readonly ParameterInfo _parameter;
        private readonly IBindingDataProvider _bindingDataProvider;
        private readonly bool _isUserTypeBinding;
        private Uri _route;
        
        public WebHookTriggerBinding(WebHookDispatcher dispatcher, ParameterInfo parameter, bool isUserTypeBinding, WebHookTriggerAttribute attribute)
        {
            _dispatcher = dispatcher;
            _parameter = parameter;
            _isUserTypeBinding = isUserTypeBinding;

            if (_isUserTypeBinding)
            {
                _bindingDataProvider = BindingDataProvider.FromType(parameter.ParameterType);
            }

            Uri baseAddress = new Uri(string.Format("http://localhost:{0}", dispatcher.Port));
            _route = FormatWebHookUri(baseAddress, attribute, parameter);
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get { return _bindingDataProvider != null ? _bindingDataProvider.Contract : null; }
        }

        public Type TriggerValueType
        {
            get { return typeof(HttpRequestMessage); }
        }

        public async Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            HttpRequestMessage request = value as HttpRequestMessage;
            if (request == null && value != null && value.GetType() == typeof(string))
            {
                // We've received an invoke string (e.g. from a Dashboard replay/invoke
                // so convert to a request
                request = FromInvokeString((string)value);
            }

            IValueProvider valueProvider = null;  
            IReadOnlyDictionary<string, object> bindingData = null;
            string invokeString = ToInvokeString(request);
            if (_isUserTypeBinding)
            {
                // For user type bindings, we deserialize the json body into an
                // instance of their type
                string json = await request.Content.ReadAsStringAsync();
                value = JsonConvert.DeserializeObject(json, _parameter.ParameterType);
                valueProvider = new WebHookUserTypeValueBinder(_parameter.ParameterType, value, invokeString);
                bindingData = _bindingDataProvider.GetBindingData(value);
            }
            else
            {
                valueProvider = new WebHookRequestValueBinder(_parameter, request, invokeString);
            }

            return new TriggerData(valueProvider, bindingData);
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
                    DefaultValue = ToInvokeString(new HttpRequestMessage(HttpMethod.Post, _route))
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

        internal static bool IsValidUserType(Type parameterType)
        {
            return parameterType.IsClass;
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

            JObject invokeObject = new JObject()
                {
                    { "url", request.RequestUri.ToString() },
                    { "body", body }
                };

            string invokeString = invokeObject.ToString();
            return invokeString;
        }

        internal static HttpRequestMessage FromInvokeString(string value)
        {
            JObject invokeObject = JObject.Parse((string)value);
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, (string)invokeObject["url"]);
            request.Content = new StringContent((string)invokeObject["body"]);

            return request;
        }

        private class WebHookTriggerParameterDescriptor : TriggerParameterDescriptor
        {
            public override string GetTriggerReason(IDictionary<string, string> arguments)
            {
                string invokeString = null;
                if (arguments != null && arguments.TryGetValue(Name, out invokeString))
                {
                    HttpRequestMessage request = WebHookTriggerBinding.FromInvokeString(invokeString);
                    return string.Format("WebHook triggered by request to '{0}'", request.RequestUri);
                }
                return null;
            }
        }

        /// <summary>
        /// ValueBinder for all our built in supported Types
        /// </summary>
        private class WebHookRequestValueBinder : StreamValueBinder
        {
            private readonly ParameterInfo _parameter;
            private readonly HttpRequestMessage _request;
            private readonly string _invokeString;

            public WebHookRequestValueBinder(ParameterInfo parameter, HttpRequestMessage request, string invokeString)
                : base(parameter)
            {
                _parameter = parameter;
                _request = request;
                _invokeString = invokeString;
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
                return _invokeString;
            }
        }

        /// <summary>
        /// ValueBinder for custom user Types
        /// </summary>
        private class WebHookUserTypeValueBinder : IValueProvider
        {
            private readonly Type _type;
            private readonly object _value;
            private readonly string _invokeString;

            public WebHookUserTypeValueBinder(Type type, object value, string invokeString)
            {
                _type = type;
                _value = value;
                _invokeString = invokeString;
            }

            public Type Type
            {
                get
                {
                    return _type;
                }
            }

            public object GetValue()
            {
                return _value;
            }

            public string ToInvokeString()
            {
                return _invokeString;
            }
        }
    }
}
