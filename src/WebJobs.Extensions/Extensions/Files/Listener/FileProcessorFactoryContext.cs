﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;

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
        /// <param name="logger">The <see cref="ILogger"/>.</param>
        public FileProcessorFactoryContext(FilesConfiguration config, FileTriggerAttribute attribute, ITriggeredFunctionExecutor executor, ILogger logger)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the <see cref="FilesConfiguration"/>
        /// </summary>
        public FilesConfiguration Config { get; private set; }

        /// <summary>
        /// Gets the <see cref="FileTriggerAttribute"/>
        /// </summary>
        public FileTriggerAttribute Attribute { get; private set; }

        /// <summary>
        /// Gets the function executor
        /// </summary>
        public ITriggeredFunctionExecutor Executor { get; private set; }

        /// <summary>
        /// Gets the <see cref="ILogger"/>.
        /// </summary>
        public ILogger Logger { get; private set; }
    }
}
