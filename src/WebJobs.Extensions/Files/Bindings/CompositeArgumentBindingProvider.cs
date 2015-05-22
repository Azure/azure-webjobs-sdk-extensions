using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace WebJobs.Extensions.Files.Bindings
{
    internal class CompositeArgumentBindingProvider : IFileTriggerArgumentBindingProvider
    {
        private readonly IEnumerable<IFileTriggerArgumentBindingProvider> _providers;

        public CompositeArgumentBindingProvider(params IFileTriggerArgumentBindingProvider[] providers)
        {
            _providers = providers;
        }

        public IArgumentBinding<FileSystemEventArgs> TryCreate(ParameterInfo parameter)
        {
            foreach (IFileTriggerArgumentBindingProvider provider in _providers)
            {
                IArgumentBinding<FileSystemEventArgs> binding = provider.TryCreate(parameter);

                if (binding != null)
                {
                    return binding;
                }
            }

            return null;
        }
    }
}
