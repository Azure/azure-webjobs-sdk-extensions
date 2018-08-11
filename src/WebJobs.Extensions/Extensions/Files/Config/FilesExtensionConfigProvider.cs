// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Files.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Files.Listener;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Extensions.Files
{
    [Extension("Files")]
    internal class FilesExtensionConfigProvider : IExtensionConfigProvider, IConverter<FileAttribute, Stream>
    {
        private readonly IOptions<FilesOptions> _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IFileProcessorFactory _fileProcessorFactory;

        public FilesExtensionConfigProvider(IOptions<FilesOptions> options, ILoggerFactory loggerFactory, IFileProcessorFactory fileProcessorFactory)
        {
            _options = options;
            _loggerFactory = loggerFactory;
            _fileProcessorFactory = fileProcessorFactory;
        }

        private FileInfo GetFileInfo(FileAttribute attribute)
        {
            string boundFileName = attribute.Path;
            string filePath = Path.Combine(_options.Value.RootPath, boundFileName);
            FileInfo fileInfo = new FileInfo(filePath);
            return fileInfo;
        }

        public FileStream GetFileStream(FileAttribute attribute)
        {
            var fileInfo = GetFileInfo(attribute);
            if ((attribute.Access == FileAccess.Read) && !fileInfo.Exists)
            {
                return null;
            }

            return fileInfo.Open(attribute.Mode, attribute.Access);
        }

        public Stream Convert(FileAttribute attribute)
        {
            return GetFileStream(attribute);
        }

        public void Initialize(ExtensionConfigContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var rule = context.AddBindingRule<FileAttribute>();
            rule.BindToInput<FileInfo>(this.GetFileInfo);
            rule.BindToInput<FileStream>(this.GetFileStream);
            rule.BindToStream(this, FileAccess.ReadWrite);

            // Triggers
            var rule2 = context.AddBindingRule<FileTriggerAttribute>();
            rule2.BindToTrigger<FileSystemEventArgs>(new FileTriggerAttributeBindingProvider(_options, _loggerFactory, _fileProcessorFactory));

            rule2.AddConverter<string, FileSystemEventArgs>(str => FileTriggerBinding.GetFileArgsFromString(str));
            rule2.AddConverter<FileSystemEventArgs, Stream>(args => File.OpenRead(args.FullPath));
        }
    }
}
