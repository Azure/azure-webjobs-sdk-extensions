using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
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
