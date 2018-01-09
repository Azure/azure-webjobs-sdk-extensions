// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Files.Bindings;
using Microsoft.Azure.WebJobs.Host.Config;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Extension methods for registering the Files extension.
    /// </summary>
    public static class FilesJobHostConfigurationExtensions
    {
        /// <summary>
        /// Enables use of File System extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        public static void UseFiles(this JobHostConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            FilesConfiguration filesConfiguration = new FilesConfiguration();

            config.UseFiles(filesConfiguration);
        }

        /// <summary>
        /// Enables use of File System extensions
        /// </summary>
        /// <param name="config">The <see cref="JobHostConfiguration"/> to configure.</param>
        /// <param name="filesConfig">The <see cref="FilesConfiguration"></see> to use./></param>
        public static void UseFiles(this JobHostConfiguration config, FilesConfiguration filesConfig)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (filesConfig == null)
            {
                throw new ArgumentNullException("filesConfig");
            }

            config.RegisterExtensionConfigProvider(new FilesExtensionConfig(filesConfig));
        }

        private class FilesExtensionConfig : IExtensionConfigProvider,
            IConverter<FileAttribute, Stream>
        {
            private FilesConfiguration _filesConfig;

            public FilesExtensionConfig(FilesConfiguration filesConfig)
            {
                _filesConfig = filesConfig;
            }

            private FileInfo GetFileInfo(FileAttribute attribute)
            {
                string boundFileName = attribute.Path;
                string filePath = Path.Combine(_filesConfig.RootPath, boundFileName);
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
                rule2.BindToTrigger<FileSystemEventArgs>(new FileTriggerAttributeBindingProvider(_filesConfig, context.Config.LoggerFactory));

                rule2.AddConverter<string, FileSystemEventArgs>(str => FileTriggerBinding.GetFileArgsFromString(str));
                rule2.AddConverter<FileSystemEventArgs, Stream>(args => File.OpenRead(args.FullPath));
            }
        }
    }
}
