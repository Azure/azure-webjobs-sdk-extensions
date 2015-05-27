using System;
using System.IO;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace WebJobs.Extensions.Files.Listener
{
    /// <summary>
    /// Context input for <see cref="IFileProcessorFactory"/>
    /// </summary>
    public class FileProcessorFactoryContext
    {
        public FileProcessorFactoryContext(FilesConfiguration config, FileTriggerAttribute attribute, ITriggeredFunctionExecutor<FileSystemEventArgs> executor)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (attribute == null)
            {
                throw new ArgumentNullException("attribute");
            }
            if (executor == null)
            {
                throw new ArgumentNullException("executor");
            }

            Config = config;
            Attribute = attribute;
            Executor = executor;
        }

        public FilesConfiguration Config { get; private set; }
        public FileTriggerAttribute Attribute { get; private set; }
        public ITriggeredFunctionExecutor<FileSystemEventArgs> Executor { get; private set; }
    }
}
