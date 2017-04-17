// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.Devices;
using Microsoft.Azure.WebJobs.Extensions.IoTHub;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind to an Azure IoTHub.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="ICollector{T}"/></description></item>
    /// <item><description><see cref="IAsyncCollector{T}"/></description></item>
    /// <item><description>out T</description></item>
    /// <item><description>out T[]</description></item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class IoTHubAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public IoTHubAttribute()
        {
        }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="deviceId">The device id to send to.</param>
        public IoTHubAttribute(string deviceId)
        {
            DeviceId = deviceId;
        }

        /// <summary>
        /// The id of the device to send the message to.
        /// May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string DeviceId { get; private set; }

        /// <summary>
        /// Optional. A string value indicating the app setting to use as the DocumentDB connection string.
        /// </summary>
        [AppSetting(Default = "AzureWebJobsIoTHub")]
        public string ConnectionString { get; set; }
    }
}
