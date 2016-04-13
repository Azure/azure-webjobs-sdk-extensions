﻿using System;

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
        /// <param name="pollIntervalInSeconds">The poll interval in seconds.</param>
        public ApiHubFileTriggerAttribute(string key, string path, int pollIntervalInSeconds = 0)
            : base(key, path)
        {
            this.PollIntervalInSeconds = pollIntervalInSeconds;
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
