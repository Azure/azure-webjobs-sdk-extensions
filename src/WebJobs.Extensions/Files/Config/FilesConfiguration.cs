using System;
using System.IO;
using WebJobs.Extensions.Files.Listener;

namespace WebJobs.Extensions.Files
{
    /// <summary>
    /// Configuration object for <see cref="FileTriggerAttribute"/> decorated job functions.
    /// </summary>
    public class FilesConfiguration
    {
        public FilesConfiguration()
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
        /// Gets or sets the root path where file monitoring will occur.
        /// </summary>
        public string RootPath { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IFileProcessorFactory"/> that will be used
        /// to create <see cref="FileProcessor"/> instances.
        /// </summary>
        public IFileProcessorFactory ProcessorFactory { get; set; }
    }
}
