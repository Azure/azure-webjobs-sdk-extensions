// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;
using static Microsoft.Azure.WebJobs.Extensions.Http.HttpTriggerAttributeBindingProvider.HttpTriggerBinding;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    // support allowing binding any non-attributed HttpRequest parameter to the incoming http request.     
    //    Foo([HttpTrigger] Poco x, HttpRequest y); 
    internal class HttpDirectRequestBindingProvider : IBindingProvider
    {
        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            if (parameter.ParameterType == typeof(HttpRequest))
            {
                // Not already claimed by another trigger?
                if (!HasBindingAttributes(parameter))
                {
                    return Task.FromResult<IBinding>(new HttpRequestBinding());
                }
            }
            return Task.FromResult<IBinding>(null);
        }

        private static bool HasBindingAttributes(ParameterInfo parameter)
        {
            foreach (Attribute attr in parameter.GetCustomAttributes(false))
            {
                if (IsBindingAttribute(attr))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsBindingAttribute(Attribute attribute)
        {
            return attribute.GetType().GetCustomAttribute<BindingAttribute>() != null;
        }

        public class HttpRequestBinding : IBinding
        {
            public bool FromAttribute
            {
                get
                {
                    return false;
                }
            }

            public Task<IValueProvider> BindAsync(BindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }
                var request = context.BindingData[HttpTriggerAttributeBindingProvider.RequestBindingName];

                return BindAsync(request, context.ValueContext);
            }

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                var request = value as HttpRequest;
                if (request != null)
                {
                    var binding = new SimpleValueProvider(typeof(HttpRequest), request, "request");
                    return Task.FromResult<IValueProvider>(binding);
                }
                throw new InvalidOperationException("value must be an HttpRequest");
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor
                {
                    Name = "request"
                };
            }
        }
    }
}