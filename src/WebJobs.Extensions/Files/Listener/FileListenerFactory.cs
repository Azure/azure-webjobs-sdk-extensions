// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Files.Listeners;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using WebJobs.Extensions.Files;
using WebJobs.Extensions.Files.Listener;

namespace Microsoft.Azure.WebJobs.Fiels.Listeners
{
    internal class FileListenerFactory : IListenerFactory
    {
        private readonly FileTriggerAttribute _attribute;
        private readonly ITriggeredFunctionExecutor<FileSystemEventArgs> _executor;
        private readonly FilesConfiguration _config;

        public FileListenerFactory(FilesConfiguration config, FileTriggerAttribute attribute, ITriggeredFunctionExecutor<FileSystemEventArgs> executor)
        {
            _config = config;
            _attribute = attribute;
            _executor = executor;
        }

        public async Task<IListener> CreateAsync(ListenerFactoryContext context)
        {
            FileTriggerExecutor triggerExecutor = new FileTriggerExecutor(_executor);
            return new FileListener(_config, _attribute, triggerExecutor);
        }
    }
}
