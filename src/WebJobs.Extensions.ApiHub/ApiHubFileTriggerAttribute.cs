﻿namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute that points to the file on SAAS file trigger
    /// </summary>
    public class ApiHubFileTriggerAttribute : ApiHubFileAttribute
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
    }
}
