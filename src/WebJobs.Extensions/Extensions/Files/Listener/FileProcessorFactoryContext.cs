// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs.Host;
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
        /// <param name="trace">The <see cref="TraceWriter"/>.</param>
        public FileProcessorFactoryContext(FilesConfiguration config, FileTriggerAttribute attribute, ITriggeredFunctionExecutor executor, TraceWriter trace)
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
            if (trace == null)
            {
                throw new ArgumentNullException("trace");
            }

            Config = config;
            Attribute = attribute;
            Executor = executor;
            Trace = trace;
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
        public ITriggeredFunctionExecutor Executor { get; private set; }

        /// <summary>
        /// Gets the <see cref="TraceWriter"/>.
        /// </summary>
        public TraceWriter Trace { get; private set; }
    }
}
