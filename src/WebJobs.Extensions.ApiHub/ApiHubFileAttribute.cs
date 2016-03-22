using System;
using System.IO;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;

namespace Microsoft.Azure.WebJobs
{
    public class ApiHubFileAttribute : Attribute, IFileAttribute
    {
        public ApiHubFileAttribute(string key, string path, FileAccess access = FileAccess.Read)
        {
            this.Key = key;
            this.Path = path;
            this.Access = access;
        }

        public string Key { get; set; }

        public FileAccess Access { get; set; }

        public string Path { get; set; }
    }
}
