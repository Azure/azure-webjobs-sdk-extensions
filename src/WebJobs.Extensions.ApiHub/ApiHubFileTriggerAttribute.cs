using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute that points to the file on SAAS file trigger
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class ApiHubFileTriggerAttribute : ApiHubFileAttribute
    {
        /// <summary>
        ///  Create an instance of an attribute that points to the file on SAAS file trigger
        /// </summary>
        /// <param name="key">App settings key name that have the connections string</param>
        /// <param name="path">Relative path to the file <example>/folder/subfolder/file.txt</example> </param>
        public ApiHubFileTriggerAttribute(string key, string path)
            : base(key, path)
        {
        }

        /// <summary>
        /// Gets or sets the poll interval in seconds.
        /// </summary>
        /// <value>
        /// The poll interval in seconds.
        /// </value>
        public int PollIntervalInSeconds { get; set; }
    }
}
