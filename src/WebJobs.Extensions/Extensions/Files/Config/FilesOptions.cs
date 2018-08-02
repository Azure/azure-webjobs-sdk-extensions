// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Extensions.Files.Listener;

namespace Microsoft.Azure.WebJobs.Extensions.Files
{
    /// <summary>
    /// Configuration object for the Files extension. Governs the behavior of functions attributed
    /// with <see cref="FileTriggerAttribute"/> and <see cref="FileAttribute"/>.
    /// </summary>
    public class FilesOptions
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public FilesOptions()
        {
            // default to the D:\HOME\DATA directory when running in Azure WebApps
            string home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                RootPath = Path.Combine(home, "data");
            }

            ProcessorFactory = new DefaultFileProcessorFactory();
        }

        /// <summary>
        /// Gets or sets the root path where files will monitoring
        /// and created.
        /// </summary>
        public string RootPath { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IFileProcessorFactory"/> that will be used
        /// to create <see cref="FileProcessor"/> instances.
        /// </summary>
        public IFileProcessorFactory ProcessorFactory { get; set; }
    }
}
