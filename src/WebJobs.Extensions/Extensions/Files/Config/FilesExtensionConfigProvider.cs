// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Files.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Extensions.Files
{
    [Extension("Files")]
    internal class FilesExtensionConfigProvider : IExtensionConfigProvider
    {
        private readonly IOptions<FilesOptions> _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IFileProcessorFactory _fileProcessorFactory;
        private readonly INameResolver _nameResolver;

        public FilesExtensionConfigProvider(IOptions<FilesOptions> options, ILoggerFactory loggerFactory, IFileProcessorFactory fileProcessorFactory, INameResolver nameResolver)
        {
            _options = options;
            _loggerFactory = loggerFactory;
            _fileProcessorFactory = fileProcessorFactory;
            _nameResolver = nameResolver;
        }

        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var fileBindingProvider = new FileAttributeBindingProvider(_options, _nameResolver);
            context.AddBindingRule<FileAttribute>()
                .Bind(fileBindingProvider);

            var triggerBindingProvider = new FileTriggerAttributeBindingProvider(_options, _loggerFactory, _fileProcessorFactory);
            var triggerRule = context.AddBindingRule<FileTriggerAttribute>();
            triggerRule.BindToTrigger(triggerBindingProvider);
            triggerRule.AddConverter<string, FileSystemEventArgs>(p => FileTriggerBinding.GetFileArgsFromString(p));
            triggerRule.AddConverter<FileSystemEventArgs, Stream>(p => File.OpenRead(p.FullPath));
        }
    }
}
