using System;
using System.IO;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Listener
{
    /// <summary>
    /// Context input for <see cref="IFileProcessorFactory"/>
    /// </summary>
    public class FileProcessorFactoryContext
    {
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="config">The <see cref="FilesConfiguration"/></param>
        /// <param name="attribute">The <see cref="FileTriggerAttribute"/></param>
        /// <param name="executor">The function executor.</param>
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

        /// <summary>
        /// The <see cref="FilesConfiguration"/>
        /// </summary>
        public FilesConfiguration Config { get; private set; }

        /// <summary>
        /// The <see cref="FileTriggerAttribute"/>
        /// </summary>
        public FileTriggerAttribute Attribute { get; private set; }

        /// <summary>
        /// Gets the function executor
        /// </summary>
        public ITriggeredFunctionExecutor<FileSystemEventArgs> Executor { get; private set; }
    }
}
