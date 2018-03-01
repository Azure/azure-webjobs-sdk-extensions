// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    internal class AuthenticatedUserBindingProvider : IBindingProvider
    {
        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (context.Parameter.ParameterType != typeof(AuthenticatedUser))
            {
                return Task.FromResult<IBinding>(null);
            }

            return Task.FromResult<IBinding>(new AuthenticatedUserBinding(context.Parameter));
        }

        private class AuthenticatedUserBinding : IBinding
        {
            private readonly ParameterInfo _parameter;

            public AuthenticatedUserBinding(ParameterInfo parameter)
            {
                _parameter = parameter;
            }

            public bool FromAttribute => false;

            public Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
            {
                // TODO: Don't know what to do with this without binding data, or if it 
                // is even needed in our use case.
                throw new NotImplementedException("This method doesn't have binding data.");
            }

            public Task<IValueProvider> BindAsync(BindingContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException("context");
                }

                return BindAsync(context.BindingData);
            }

            private static Task<IValueProvider> BindAsync(IReadOnlyDictionary<string, object> bindingData)
            {
                return Task.FromResult<IValueProvider>(new AuthenticatedUserValueProvider(bindingData));
            }

            public ParameterDescriptor ToParameterDescriptor()
            {
                return new ParameterDescriptor
                {
                    Name = _parameter.Name,
                    DisplayHints = new ParameterDisplayHints
                    {
                        Description = "AuthenticatedUser"
                    }
                };
            }

            private class AuthenticatedUserValueProvider : IValueProvider
            {
                private static HttpClient _client = new HttpClient();
                private IReadOnlyDictionary<string, object> _bindingData;

                public AuthenticatedUserValueProvider(IReadOnlyDictionary<string, object> bindingData)
                {
                    _bindingData = bindingData;
                }

                public Type Type => typeof(AuthenticatedUser);

                public async Task<object> GetValueAsync()
                {
                    var initialRequest = _bindingData.Values.FirstOrDefault(val => val.GetType() == typeof(HttpRequestMessage)) as HttpRequestMessage;
                    if (initialRequest == null)
                    {
                        throw new InvalidOperationException("Cannot determine authenticated user without HttpTrigger.");
                    }

                    var authorizationHeader = GetAuthorizationHeaderValue(initialRequest);
                    if (authorizationHeader != null)
                    {
                        return await GetAuthenticatedUserFromEasyAuth(authorizationHeader);
                    }
                    else
                    {
                        return GetAuthenticatedUserFromFunctionKey();
                    }   
                }

                private static string GetAuthorizationHeaderValue(HttpRequestMessage initialRequest)
                {
                    var idTokenHeaders = initialRequest.Headers.Where(header => header.Key.EndsWith("id-token", StringComparison.OrdinalIgnoreCase));
                    if (!idTokenHeaders.Any())
                    {
                        return null;
                    }
                    else
                    {
                        var idTokenHeader = idTokenHeaders.First();
                        var idTokenValue = idTokenHeader.Value.First();
                        return "Bearer " + idTokenValue;
                    }
                }

                private async Task<AuthenticatedUser> GetAuthenticatedUserFromEasyAuth(string authorizationHeader)
                {
                    string hostname = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
                    var authUri = "https://" + hostname + "/.auth/me";
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, authUri);
                    request.Headers.Add("Authorization", authorizationHeader);
                    HttpResponseMessage response = await _client.SendAsync(request);

                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // TODO: Determine if an exception is the most appropriate use case here
                        // especially once we start dealing with AND/OR of multiple AuthLevel enums
                        throw new InvalidOperationException("No user was authenticated.");
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        //Same concern as the unauthorized case
                        throw new InvalidOperationException("Authentication/Authorization is not enabled.");
                    }
                    else if (response.IsSuccessStatusCode)
                    {
                        string authenticatedUserJson = await response.Content.ReadAsStringAsync();
                        return AuthenticatedUser.DeserializeJson(authenticatedUserJson);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Do not know how to handle response status code {response.StatusCode} from {authUri}");
                    }
                }

                private static AuthenticatedUser GetAuthenticatedUserFromFunctionKey()
                {
                    throw new NotImplementedException("TODO: Have not worked out what information I can/should populate here.");
                }

                public string ToInvokeString()
                {
                    //TODO: what goes here?
                    return string.Empty;
                }
            }
        }
    }
}
