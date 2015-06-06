using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.Files
{
    internal class FileTriggerParameterDescriptor : TriggerParameterDescriptor
    {
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
