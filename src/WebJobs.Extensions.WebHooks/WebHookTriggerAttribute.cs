// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to declare that a job function should be triggered
    /// by incoming HTTP messages.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="System.Net.Http.HttpRequestMessage"/></description></item>
    /// <item><description><see cref="WebHookContext"/></description></item>
    /// <item><description><see cref="string"/></description></item>
    /// <item><description><see cref="T:byte[]"/></description></item>
    /// <item><description><see cref="System.IO.Stream"/></description></item>
    /// <item><description><see cref="System.IO.TextReader"/></description></item>
    /// <item><description><see cref="System.IO.StreamReader"/></description></item>
    /// <item><description>A user-defined type (serialized as JSON)</description></item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class WebHookTriggerAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="route">The optional route that the function should be triggered on.
        /// When not explicitly set, the route will be defaulted by convention to 
        /// {ClassName}/{MethodName}.</param>
        public WebHookTriggerAttribute(string route = null)
        {
            Route = route;
        }

        /// <summary>
        /// Gets the WebHook route the function will be triggered on.
        /// </summary>
        public string Route { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether values should come
        /// from Uri parameters or the request body, when binding to a
        /// user Type. By default, values come from the POST body.
        /// </summary>
        public bool FromUri { get; set; }
    }
}
