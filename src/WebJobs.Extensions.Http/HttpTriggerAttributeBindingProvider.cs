// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    internal class HttpTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        internal const string HttpQueryKey = "Query";
        internal const string HttpHeadersKey = "Headers";

        public HttpTriggerAttributeBindingProvider()
        {
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            HttpTriggerAttribute attribute = parameter.GetCustomAttribute<HttpTriggerAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            // Can bind to user types, HttpRequestMessage, object (for dynamic binding support) and all the Read
            // Types supported by StreamValueBinder
            IEnumerable<Type> supportedTypes = StreamValueBinder.GetSupportedTypes(FileAccess.Read)
                .Union(new Type[] { typeof(HttpRequest), typeof(object), typeof(HttpRequestMessage) });
            bool isSupportedTypeBinding = ValueBinder.MatchParameterType(parameter, supportedTypes);
            bool isUserTypeBinding = !isSupportedTypeBinding && IsValidUserType(parameter.ParameterType);
            if (!isSupportedTypeBinding && !isUserTypeBinding)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    "Can't bind HttpTriggerAttribute to type '{0}'.", parameter.ParameterType));
            }

            return Task.FromResult<ITriggerBinding>(new HttpTriggerBinding(attribute, context.Parameter, isUserTypeBinding));
        }

        private static bool IsValidUserType(Type type)
        {
            return !type.IsInterface && !type.IsPrimitive && !(type.Namespace == "System");
        }

        internal class HttpTriggerBinding : ITriggerBinding
        {
            private readonly ParameterInfo _parameter;
            private readonly IBindingDataProvider _bindingDataProvider;
            private readonly bool _isUserTypeBinding;
            private readonly Dictionary<string, Type> _bindingDataContract;
            
            public HttpTriggerBinding(HttpTriggerAttribute attribute, ParameterInfo parameter, bool isUserTypeBinding)
            {
                _parameter = parameter;
                _isUserTypeBinding = isUserTypeBinding;

                if (_isUserTypeBinding)
                {
                    // Create the BindingDataProvider from the user Type. The BindingDataProvider
                    // is used to define the binding parameters that the binding exposes to other
                    // bindings (i.e. the properties of the POCO can be bound to by other bindings).
                    // It is also used to extract the binding data from an instance of the Type.
                    _bindingDataProvider = BindingDataProvider.FromType(parameter.ParameterType);
                }

                _bindingDataContract = GetBindingDataContract(attribute, parameter);
            }

            public IReadOnlyDictionary<string, Type> BindingDataContract
            {
                get
                {
                    return _bindingDataContract;
                }
            }

            public Type TriggerValueType
            {
                get { return typeof(HttpRequestMessage); }
            }

            public async Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
            {
                HttpRequest request = value as HttpRequest;
                if (request == null)
                {
                    throw new NotSupportedException("An HttpRequestMessage is required");
                }

                IValueProvider valueProvider = null;
                object poco = null;
                IReadOnlyDictionary<string, object> userTypeBindingData = null;
                string invokeString = ToInvokeString(request);
                if (_isUserTypeBinding)
                {
                    valueProvider = await CreateUserTypeValueProvider(request, invokeString);
                    if (_bindingDataProvider != null)
                    {
                        // some binding data is defined by the user type
                        // the provider might be null if the Type is invalid, or if the Type
                        // has no public properties to bind to
                        poco = await valueProvider.GetValueAsync();
                        userTypeBindingData = _bindingDataProvider.GetBindingData(poco);
                    }
                }
                else
                {
                    valueProvider = new HttpRequestValueBinder(_parameter, request, invokeString);
                }

                // create a modifiable collection of binding data and
                // copy in any initial binding data from the poco
                var aggregateBindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                aggregateBindingData.AddRange(userTypeBindingData);

                // Apply additional binding data coming from request route, query params, etc.
                var requestBindingData = await GetRequestBindingDataAsync(request, _bindingDataContract);
                aggregateBindingData.AddRange(requestBindingData);

                // apply binding data to the user type
                if (poco != null && aggregateBindingData.Count > 0)
                {
                    ApplyBindingData(poco, aggregateBindingData);
                }

                return new TriggerData(valueProvider, aggregateBindingData);
            }

            public static string ToInvokeString(HttpRequest request)
            {
                // For display in the Dashboard, we want to be sure we don't log
                // any sensitive portions of the URI (e.g. query params, headers, etc.)
                var builder = new UriBuilder
                {
                    Host = request.Host.Host,
                    Path = request.Path,
                    Scheme = request.Scheme
                };

                if (request.Host.Port.HasValue)
                {
                    builder.Port = request.Host.Port.Value;
                }
                

                return $"Method: {request.Method}, Uri: {builder.Uri.GetLeftPart(UriPartial.Path)}";
            }

            public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                return Task.FromResult<IListener>(new NullListener());
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new TriggerParameterDescriptor
                {
                    Name = _parameter.Name
                };
            }

            internal static void ApplyBindingData(object target, IDictionary<string, object> bindingData)
            {
                var propertyHelpers = PropertyHelper.GetProperties(target).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var pair in bindingData)
                {
                    PropertyHelper propertyHelper;
                    if (propertyHelpers.TryGetValue(pair.Key, out propertyHelper) &&
                        propertyHelper.Property.CanWrite)
                    {
                        object value = pair.Value;
                        value = ConvertValueIfNecessary(value, propertyHelper.Property.PropertyType);
                        propertyHelper.SetValue(target, value);
                    }
                }
            }

            /// <summary>
            /// Gets the static strongly typed binding data contract
            /// </summary>
            internal Dictionary<string, Type> GetBindingDataContract(HttpTriggerAttribute attribute, ParameterInfo parameter)
            {
                // add contract members for POCO binding properties if binding to a POCO
                var aggregateDataContract = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                if (_isUserTypeBinding && _bindingDataProvider?.Contract != null)
                {
                    aggregateDataContract.AddRange(_bindingDataProvider.Contract);
                }

                // add contract members for any route parameters
                if (!string.IsNullOrEmpty(attribute.Route))
                {
                    var routeParameters = TemplateParser.Parse(attribute.Route).Parameters; //_httpRouteFactory.GetRouteParameters(attribute.Route);
                    var parameters = ((MethodInfo)parameter.Member).GetParameters().ToDictionary(p => p.Name, p => p.ParameterType, StringComparer.OrdinalIgnoreCase);
                    foreach (TemplatePart routeParameter in routeParameters)
                    {
                        // don't override if the contract already includes a name
                        if (!aggregateDataContract.ContainsKey(routeParameter.Name))
                        {
                            // if there is a method parameter mapped to this parameter
                            // derive the Type from that
                            Type type;
                            if (!parameters.TryGetValue(routeParameter.Name, out type))
                            {
                                type = typeof(string);
                            }
                            aggregateDataContract[routeParameter.Name] = type;
                        }
                    }
                }

                // add headers collection to the contract
                if (!aggregateDataContract.ContainsKey(HttpHeadersKey))
                {
                    aggregateDataContract.Add(HttpHeadersKey, typeof(IDictionary<string, string>));
                }

                // add query parameter collection to binding contract
                if (!aggregateDataContract.ContainsKey(HttpQueryKey))
                {
                    aggregateDataContract.Add(HttpQueryKey, typeof(IDictionary<string, string>));
                }

                return aggregateDataContract;
            }

            internal static async Task<IReadOnlyDictionary<string, object>> GetRequestBindingDataAsync(HttpRequest request, Dictionary<string, Type> bindingDataContract = null)
            {
                // apply binding data from request body if present
                var bindingData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                if (request.ContentLength != null && request.ContentLength > 0)
                {
                    string body = await request.ReadAsStringAsync();
                    Utility.ApplyBindingData(body, bindingData);
                }

                // apply binding data from the query string
                foreach (var pair in request.Query)
                {
                    if (string.Compare("code", pair.Key, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        // skip any system parameters that should not be bound to
                        continue;
                    }
                    bindingData[pair.Key] = pair.Value;
                }

                // apply binding data from route parameters
                object value = null;
                if (request.HttpContext.Items.TryGetValue(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, out value))
                {
                    var routeBindingData = (Dictionary<string, object>)value;
                    foreach (var pair in routeBindingData)
                    {
                        // if we have a static binding contract that maps to this parameter
                        // derive the type from that contract mapping and perform any
                        // necessary conversion
                        value = pair.Value;
                        Type type = null;
                        if (bindingDataContract != null &&
                            bindingDataContract.TryGetValue(pair.Key, out type))
                        {
                            value = ConvertValueIfNecessary(value, type);
                        }

                        bindingData[pair.Key] = value;
                    }
                }

                // add query parameter collection to binding data
                if (!bindingData.ContainsKey(HttpQueryKey))
                {
                    bindingData[HttpQueryKey] = request.GetQueryParameterDictionary();
                }

                // add headers collection to binding data
                if (!bindingData.ContainsKey(HttpHeadersKey))
                {
                    
                    bindingData[HttpHeadersKey] = request.Headers.Select(h => string.Format("{0} : {1}", h.Key, h.Value));
                }

                return bindingData;
            }

            private Task<IValueProvider> CreateUserTypeValueProvider(HttpRequest request, string invokeString)
            {
                // First check to see if the WebHook data has already been deserialized,
                // otherwise read from the request body if present
                // TODO: FACAVAL - Pending WebHooks support
                //object value = null;
                //if (!request.HttpContext.Items.TryGetValue(HttpExtensionConstants.AzureWebJobsWebHookDataKey, out value))
                //{
                //    //if (request.ReadAsAsync() .re.Content != null && request.Content.Headers.ContentLength > 0)
                //    if (false)
                //    {
                //        // deserialize from message body
                //        //value = await request.ReadAsAsync(_parameter.ParameterType);
                //    }
                //}

                //if (value == null)
                //{
                //    // create an empty object
                //    
                //}

                object value = Activator.CreateInstance(_parameter.ParameterType);
                return Task.FromResult<IValueProvider>(new SimpleValueProvider(_parameter.ParameterType, value, invokeString));
            }

            private static object ConvertValueIfNecessary(object value, Type targetType)
            {
                if (value != null && !targetType.IsAssignableFrom(value.GetType()))
                {
                    var jObject = value as JObject;
                    if (jObject != null)
                    {
                        value = jObject.ToObject(targetType);
                    }
                    else
                    {
                        // if the type is nullable, we only need to convert to the
                        // correct underlying type
                        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                        value = Convert.ChangeType(value, targetType);
                    }
                }

                return value;
            }

            /// <summary>
            /// ValueBinder for all our built in supported Types
            /// </summary>
            private class HttpRequestValueBinder : StreamValueBinder
            {
                private readonly ParameterInfo _parameter;
                private readonly HttpRequest _request;
                private readonly string _invokeString;

                public HttpRequestValueBinder(ParameterInfo parameter, HttpRequest request, string invokeString)
                    : base(parameter)
                {
                    _parameter = parameter;
                    _request = request;
                    _invokeString = invokeString;
                }

                public override async Task<object> GetValueAsync()
                {
                    if (_parameter.ParameterType == typeof(HttpRequest))
                    {
                        return _request;
                    }
                    else if (_parameter.ParameterType == typeof(object))
                    {
                        // for dynamic, we read as an object, which will actually return
                        // a JObject which is dynamic
                        // TODO: FACACAL
                      //  return await _request.Content.ReadAsAsync<object>();
                    }

                    return await base.GetValueAsync();
                }

                protected override Stream GetStream()
                {
                    _request.EnableRewind();

                    Stream stream = _request.Body;

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

            private class SimpleValueProvider : IValueProvider
            {
                private readonly Type _type;
                private readonly object _value;
                private readonly string _invokeString;

                public SimpleValueProvider(Type type, object value, string invokeString)
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

                public Task<object> GetValueAsync()
                {
                    return Task.FromResult(_value);
                }

                public string ToInvokeString()
                {
                    return _invokeString;
                }
            }

            private class NullListener : IListener
            {
                public void Cancel()
                {
                }

                public void Dispose()
                {
                }

                public Task StartAsync(CancellationToken cancellationToken)
                {
                    return Task.CompletedTask;
                }

                public Task StopAsync(CancellationToken cancellationToken)
                {
                    return Task.CompletedTask;
                }
            }
        }
    }
}
