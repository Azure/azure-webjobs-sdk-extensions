using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Common.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Files.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.Files
{
    internal class FileTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        // Define the supported argument bindings
        private static readonly IArgumentBindingProvider<FileSystemEventArgs> ArgumentBindingProvider =
            new CompositeArgumentBindingProvider<FileSystemEventArgs>(
                new FileTriggerArgumentBindingProvider<FileSystemEventArgs, FileSystemEventArgs>(),
                new FileTriggerArgumentBindingProvider<FileSystemEventArgs, FileStream>(),
                new FileTriggerArgumentBindingProvider<FileSystemEventArgs, Stream>(),
                new FileTriggerArgumentBindingProvider<FileSystemEventArgs, FileInfo>(),
                new FileTriggerArgumentBindingProvider<FileSystemEventArgs, byte[]>(),
                new FileTriggerArgumentBindingProvider<FileSystemEventArgs, string>());

        private readonly FilesConfiguration _config;

        public FileTriggerAttributeBindingProvider(FilesConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            _config = config;
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            FileTriggerAttribute fileTriggerAttribute = parameter.GetCustomAttribute<FileTriggerAttribute>(inherit: false);

            if (fileTriggerAttribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            IArgumentBinding<FileSystemEventArgs> argumentBinding = ArgumentBindingProvider.TryCreate(parameter);
            if (argumentBinding == null)
            {
                throw new InvalidOperationException(string.Format("Can't bind FileTriggerAttribute to type '{0}'.", parameter.ParameterType));
            }

            ITriggerBinding binding = new FileTriggerBinding(parameter.Name, parameter.ParameterType, argumentBinding, _config, fileTriggerAttribute);

            return Task.FromResult(binding);
        }
    }
}
