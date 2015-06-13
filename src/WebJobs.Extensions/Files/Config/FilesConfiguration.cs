using System;
using System.IO;
using Microsoft.Azure.WebJobs.Extensions.Files.Listener;

namespace Microsoft.Azure.WebJobs.Extensions.Files
{
    /// <summary>
    /// Configuration object for <see cref="FileTriggerAttribute"/> decorated job functions.
    /// </summary>
    public class FilesConfiguration
    {
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        public FilesConfiguration()
        {
            // default to the D:\HOME\DATA directory when running in Azure WebApps
            string home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
            {
                RootPath = Path.Combine(home, "data");
            }

            ProcessorFactory = new DefaultFileProcessorFactory();

            MaxDegreeOfParallelism = 5;
            MaxQueueSize = -1;
        }

        /// <summary>
        /// Gets or sets the root path where file monitoring will occur.
        /// </summary>
        public string RootPath { get; set; }

        /// <summary>
        /// Gets or sets the maximum degree of parallelism that will be used
        /// when processing files concurrently.
        /// </summary>
        /// <remarks>
        /// Files are added to an internal processing queue as file events
        /// are detected, and they're processed in parallel based on this setting.
        /// </remarks>
        public int MaxDegreeOfParallelism { get; set; }

        /// <summary>
        /// Gets or sets the bounds on the maximum number of files that
        /// can be queued up for processing at one time. When set to -1,
        /// the work queue is unbounded.
        /// </summary>
        public int MaxQueueSize { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IFileProcessorFactory"/> that will be used
        /// to create <see cref="FileProcessor"/> instances.
        /// </summary>
        public IFileProcessorFactory ProcessorFactory { get; set; }
    }
}
