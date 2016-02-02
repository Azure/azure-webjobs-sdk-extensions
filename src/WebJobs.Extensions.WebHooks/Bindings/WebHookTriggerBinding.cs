// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
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
        internal const string WebHookContextRequestKey = "WebHookContext";

        private readonly WebHookDispatcher _dispatcher;
        private readonly ParameterInfo _parameter;
        private readonly IBindingDataProvider _bindingDataProvider;
        private readonly bool _isUserTypeBinding;
        private readonly WebHookTriggerAttribute _attribute;
        private Uri _route;
        
        public WebHookTriggerBinding(WebHookDispatcher dispatcher, ParameterInfo parameter, bool isUserTypeBinding, WebHookTriggerAttribute attribute)
        {
            _dispatcher = dispatcher;
            _parameter = parameter;
            _isUserTypeBinding = isUserTypeBinding;
            _attribute = attribute;

            if (_isUserTypeBinding)
            {
                // Create the BindingDataProvider from the user Type. The BindingDataProvider
                // is used to define the binding parameters that the binding exposes to other
                // bindings (i.e. the properties of the POCO can be bound to by other bindings).
                // It is also used to extract the binding data from an instance of the Type.
                _bindingDataProvider = BindingDataProvider.FromType(parameter.ParameterType);
            }

            Uri baseAddress = new Uri(string.Format("http://localhost:{0}", dispatcher.Port));
            _route = FormatWebHookUri(baseAddress, attribute, parameter);
        }

        public IReadOnlyDictionary<string, Type> BindingDataContract
        {
            get
            {
                // if we're binding to a user Type, we'll have a contract,
                // otherwise none
                return _bindingDataProvider != null ? _bindingDataProvider.Contract : null;
            }
        }

        public Type TriggerValueType
        {
            get { return typeof(HttpRequestMessage); }
        }

        public async Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            HttpRequestMessage request = value as HttpRequestMessage;
            WebHookContext webHookContext = value as WebHookContext;

            if (request == null && webHookContext != null)
            {
                request = webHookContext.Request;
            }

            if (request == null && value != null && value.GetType() == typeof(string))
            {
                // We've received an invoke string (e.g. from a Dashboard replay/invoke
                // so convert to a request
                request = FromInvokeString((string)value);
            }

            if (webHookContext == null)
            {
                webHookContext = new WebHookContext(request);
            }

            IValueProvider valueProvider = null;
            IReadOnlyDictionary<string, object> bindingData = null;
            string invokeString = ToInvokeString(request);
            if (_isUserTypeBinding)
            {
                valueProvider = await CreateUserTypeValueProvider(request, invokeString);
                if (_bindingDataProvider != null)
                {
                    // the provider might be null if the Type is invalid, or if the Type
                    // has no public properties to bind to
                    bindingData = _bindingDataProvider.GetBindingData(valueProvider.GetValue());
                }    
            }
            else
            {
                valueProvider = new WebHookRequestValueBinder(_parameter, webHookContext, invokeString);
            }

            return new TriggerData(valueProvider, bindingData);
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            WebHookListener listener = new WebHookListener(_dispatcher, _parameter, _route, context.Executor);
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

        private async Task<IValueProvider> CreateUserTypeValueProvider(HttpRequestMessage request, string invokeString)
        {
            object value = null;
            if (_attribute.FromUri)
            {
                // deserialize from Uri parameters
                NameValueCollection parameters = request.RequestUri.ParseQueryString();
                JObject intermediate = new JObject();
                foreach (var propertyName in parameters.AllKeys)
                {
                    intermediate[propertyName] = parameters[propertyName];
                }
                value = intermediate.ToObject(_parameter.ParameterType);
            }
            else
            {
                // deserialize from message body
                string json = await request.Content.ReadAsStringAsync();
                value = JsonConvert.DeserializeObject(json, _parameter.ParameterType);
            }

            return new WebHookUserTypeValueBinder(_parameter.ParameterType, value, invokeString);
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
            private readonly WebHookContext _context;
            private readonly string _invokeString;

            public WebHookRequestValueBinder(ParameterInfo parameter, WebHookContext context, string invokeString)
                : base(parameter)
            {
                _parameter = parameter;
                _context = context;
                _invokeString = invokeString;
            }

            public override object GetValue()
            {
                if (_parameter.ParameterType == typeof(HttpRequestMessage))
                {
                    return _context.Request;
                }
                else if (_parameter.ParameterType == typeof(WebHookContext))
                {
                    // when binding to WebHookContext, we add the context to the request
                    // property bag so we can pull it out later in the WebHookDispatcher to access
                    // the response value, etc.
                    _context.Request.Properties.Add(WebHookContextRequestKey, _context);

                    return _context;
                }
                return base.GetValue();
            }

            protected override Stream GetStream()
            {
                Task<Stream> task = _context.Request.Content.ReadAsStreamAsync();
                task.Wait();
                Stream stream = task.Result;

                if (stream.Position > 0 && stream.CanSeek)
                {
                    // we have to seek back to the beginning when reading as a stream,
                    // since once the Content is read somewhere else, the stream will
                    // be at the end
                    stream.Seek(0, SeekOrigin.Begin);
                }

                return stream;
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
