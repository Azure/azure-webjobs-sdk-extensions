// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Files.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Files.Converters;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.Files
{
    internal class FileTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private static readonly IFileTriggerArgumentBindingProvider ArgumentBindingProvider =
            new CompositeArgumentBindingProvider(
                new ConverterArgumentBindingProvider<FileSystemEventArgs>(new AsyncConverter<FileSystemEventArgs, FileSystemEventArgs>(new IdentityConverter<FileSystemEventArgs>())),
                new ConverterArgumentBindingProvider<FileStream>(new FileSystemEventConverter<FileStream>()),
                new ConverterArgumentBindingProvider<FileInfo>(new FileSystemEventConverter<FileInfo>()),
                new ConverterArgumentBindingProvider<byte[]>(new FileSystemEventConverter<byte[]>()),
                new ConverterArgumentBindingProvider<string>(new FileSystemEventConverter<string>()));

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

            // TODO: remove dependency on NameResolver?

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
