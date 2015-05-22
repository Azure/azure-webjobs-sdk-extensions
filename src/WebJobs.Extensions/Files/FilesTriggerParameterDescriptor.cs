using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace WebJobs.Extensions.Files
{
    internal class FilesTriggerParameterDescriptor : TriggerParameterDescriptor
    {
        public string FilePath { get; set; }

        public override string GetTriggerReason(IDictionary<string, string> arguments)
        {
            string fullPath = null;
            if (arguments != null && arguments.TryGetValue(Name, out fullPath))
            {
                return string.Format("File change detected for file '{0}'", fullPath);
            }
            return null;   
        }
    }
}
