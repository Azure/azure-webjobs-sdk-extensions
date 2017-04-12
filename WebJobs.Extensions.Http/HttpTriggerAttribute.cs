// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Description;

namespace WebJobs.Extensions.Http
{
    /// <summary>
    /// An attribute for defining http triggered functions
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    [Binding]
    public sealed class HttpTriggerAttribute : Attribute
    {
        /// <summary>
        /// The function HTTP route template.
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// The type of WebHook handled by the trigger (if handling a pre-defined WebHook).
        /// </summary>
        public string WebHookType { get; set; }

        /// <summary>
        /// The function HTTP authorization level.
        /// </summary>
        public AuthLevel AuthLevel { get; set; } = AuthLevel.Function;

        /// <summary>
        /// Allowed HTTP methods.
        /// </summary>
        public IEnumerable<HttpMethod> Methods { get; set; };
    }
}
