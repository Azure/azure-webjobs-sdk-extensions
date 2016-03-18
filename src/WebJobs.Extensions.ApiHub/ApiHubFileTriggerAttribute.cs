using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    public class ApiHubFileTriggerAttribute : ApiHubFileAttribute
    {
        public ApiHubFileTriggerAttribute(string key, string path)
            : base(key, path)
        {
        }
    }
}
