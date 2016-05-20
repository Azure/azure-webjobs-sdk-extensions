// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute that points to the file on SAAS file provider
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class ApiHubFileAttribute : Attribute, IFileAttribute
    {
        /// <summary>
        /// Create an instance of an attribute that points to the file on SAAS file provider
        /// </summary>
        /// <param name="key">App settings key name that have the connections string</param>
        /// <param name="path">Relative path to the file <example>/folder/subfolder/file.txt</example> </param>
        /// <param name="access">Type of access requests <seealso cref="FileAccess"/></param>
        public ApiHubFileAttribute(string key, string path, FileAccess access = FileAccess.Read)
        {
            this.Key = key;
            this.Path = path;
            this.Access = access;
        }

        /// <summary>
        /// Gets or sets app settings key name that have the connections string to the SAAS provider
        /// </summary>
        public string Key { get; private set; }

        /// <summary>
        /// Type of access requests <seealso cref="FileAccess"/>
        /// </summary>
        public FileAccess Access { get; private set; }

        /// <summary>
        /// Relative path to the file <example>/folder/subfolder/file.txt</example> 
        /// </summary>
        public string Path { get; set; }
    }
}
