// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to binds a parameter to an EasyTable type.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="ICollector{T}"/>, where T is either <see cref="JObject"/> or any type with a public string Id property.</description></item>
    /// <item><description><see cref="IAsyncCollector{T}"/>, where T is either <see cref="JObject"/> or any type with a public string Id property.</description></item>
    /// <item><description>out <see cref="JObject"/></description></item>
    /// <item><description>out <see cref="JObject"/>[]</description></item>
    /// <item><description>out T, where T is any Type with a public string Id property</description></item>
    /// <item><description>out T[], where T is any Type with a public string Id property</description></item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class EasyTableAttribute : Attribute
    {
        /// <summary>
        /// The name of the table to which the parameter applies.
        /// Required if using a <see cref="JObject"/> parameter; otherwise the table name is resolved
        /// by the underlying <see cref="MobileServiceClient"/> based on the item type.
        /// May include binding parameters.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// The Id of the item to retrieve from the table.
        /// May include binding parameters
        /// </summary>
        public string Id { get; set; }
    }
}