// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Triggers;
using WebJobs.Extensions.Files.Bindings;
using WebJobs.Extensions.Files.Converters;

namespace WebJobs.Extensions.Files
{
    internal class FilesTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private static readonly IFileTriggerArgumentBindingProvider _argumentBindingProvider =
            new CompositeArgumentBindingProvider(
                new ConverterArgumentBindingProvider<FileSystemEventArgs>(new AsyncConverter<FileSystemEventArgs, FileSystemEventArgs>(new IdentityConverter<FileSystemEventArgs>())),
                new ConverterArgumentBindingProvider<FileStream>(new FileSystemEventConverter<FileStream>()),
                new ConverterArgumentBindingProvider<FileInfo>(new FileSystemEventConverter<FileInfo>()),
                new ConverterArgumentBindingProvider<byte[]>(new FileSystemEventConverter<byte[]>()),
                new ConverterArgumentBindingProvider<string>(new FileSystemEventConverter<string>())
            );

        private readonly INameResolver _nameResolver;
        private readonly FilesConfiguration _config;

        public FilesTriggerAttributeBindingProvider(INameResolver nameResolver, FilesConfiguration config)
        {
            if (nameResolver == null)
            {
                throw new ArgumentNullException("nameResolver");
            }
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            _nameResolver = nameResolver;
            _config = config;
        }

        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            ParameterInfo parameter = context.Parameter;
            FileTriggerAttribute fileTriggerAttribute = parameter.GetCustomAttribute<FileTriggerAttribute>(inherit: false);

            if (fileTriggerAttribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            // TODO: remove dependency on NameResolver?

            IArgumentBinding<FileSystemEventArgs> argumentBinding = _argumentBindingProvider.TryCreate(parameter);
            if (argumentBinding == null)
            {
                throw new InvalidOperationException(string.Format("Can't bind FileTrigger to type '{0}'.", parameter.ParameterType));
            }

            ITriggerBinding binding = new FileTriggerBinding(parameter.Name, parameter.ParameterType, argumentBinding, _config, fileTriggerAttribute);

            return Task.FromResult(binding);
        }
    }
}
