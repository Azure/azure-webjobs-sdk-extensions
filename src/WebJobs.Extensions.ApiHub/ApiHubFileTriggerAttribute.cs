using System;
using Microsoft.Azure.ApiHub;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute that points to the file on SAAS file trigger
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ApiHubFileTriggerAttribute : ApiHubFileAttribute
    {
        /// <summary>
        /// Attribute used to bind a parameter to a SAAS file
        /// </summary>
        /// <param name="key">App settings key name that have the connections string</param>
        /// <param name="path">Relative path to the file <example>/folder/subfolder/file.txt</example></param>
        /// <param name="fileWatcherType">Type of the file watcher.</param>
        /// <param name="pollIntervalInSeconds">The poll interval in seconds.</param>
        public ApiHubFileTriggerAttribute(string key, string path, FileWatcherType fileWatcherType = FileWatcherType.Created, int pollIntervalInSeconds = 0)
            : base(key, path)
        {
            this.PollIntervalInSeconds = pollIntervalInSeconds;
            this.FileWatcherType = fileWatcherType;
        }

        /// <summary>
        /// Gets or sets the poll interval in seconds.
        /// </summary>
        /// <value>
        /// The poll interval in seconds.
        /// </value>
        public int PollIntervalInSeconds { get; set; }

        /// <summary>
        /// Gets or sets the type of the file watcher. Default is 'Created'.
        /// </summary>
        /// <value>
        /// The type of the file watcher.
        /// </value>
        public FileWatcherType FileWatcherType { get; set; }
    }
}
