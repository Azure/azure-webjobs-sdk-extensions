﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used for http triggered functions.
    /// </summary>
    [Binding(TriggerHandlesReturnValue = true)]
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class HttpTriggerAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public HttpTriggerAttribute()
        {
            AuthLevel = AuthorizationLevel.Function;
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>        
        /// <param name="methods">The http methods to allow.</param>
        public HttpTriggerAttribute(params string[] methods) : this()
        {
            Methods = methods;
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="authLevel">The <see cref="AuthorizationLevel"/> to apply.</param>
        /// <param name="methods">The http methods to allow.</param>
        public HttpTriggerAttribute(AuthorizationLevel authLevel, params string[] methods)
        {
            AuthLevel = authLevel;
            Methods = methods;
        }

        /// <summary>
        /// Gets or sets the route template for the function. Can include
        /// route parameters using WebApi supported syntax. If not specified,
        /// will default to the function name.
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// Gets the http methods that are supported for the function.
        /// </summary>
        public string[] Methods { get; private set; }

        /// <summary>
        /// Gets the authorization level for the function.
        /// </summary>
        public AuthorizationLevel AuthLevel { get; private set; }

        /// <summary>
        /// Gets or sets the WebHook type, if this function represents a WebHook.
        /// </summary>
        public string WebHookType { get; set; }
    }
}
