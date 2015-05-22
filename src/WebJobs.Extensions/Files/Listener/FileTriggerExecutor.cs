using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace WebJobs.Extensions.Files.Listener
{
    internal class FileTriggerExecutor
    {
        private readonly ITriggeredFunctionExecutor<FileSystemEventArgs> _innerExecutor;

        public FileTriggerExecutor(ITriggeredFunctionExecutor<FileSystemEventArgs> innerExecutor)
        {
            _innerExecutor = innerExecutor;
        }

        public async Task<bool> ExecuteAsync(FileSystemEventArgs value, CancellationToken cancellationToken)
        {
            TriggeredFunctionData<FileSystemEventArgs> input = new TriggeredFunctionData<FileSystemEventArgs>
            {
                // TODO: how to set this properly?
                ParentId = null,
                TriggerValue = value
            };

            return await _innerExecutor.TryExecuteAsync(input, cancellationToken);
        }
    }
}
