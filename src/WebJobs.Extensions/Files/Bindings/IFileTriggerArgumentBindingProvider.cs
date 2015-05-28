using System.IO;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal interface IFileTriggerArgumentBindingProvider
    {
        IArgumentBinding<FileSystemEventArgs> TryCreate(ParameterInfo parameter);
    }
}
