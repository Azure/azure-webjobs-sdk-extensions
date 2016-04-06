using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    internal class ApiHubListener : IListener
    {
        internal IFolderItem _folderSource;
        internal ITriggeredFunctionExecutor _executor;

        private const int DefaultPollIntervalInSeconds = 30;

        private IFileWatcher _poll;
        private int _pollIntervalInSeconds;

        public ApiHubListener(
            IFolderItem folder,
            ITriggeredFunctionExecutor executor,
            int pollIntervalInSeconds)
        {
            this._folderSource = folder;
            this._executor = executor;
            this._pollIntervalInSeconds = pollIntervalInSeconds;
        }

        public void Cancel()
        {
            // nop.. this is fire and forget
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            this.StopAsync(CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        public void Dispose()
        {
            // nop
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _poll = _folderSource.CreateNewFileWatcher(OnNewFile, _pollIntervalInSeconds > 0 ? _pollIntervalInSeconds : DefaultPollIntervalInSeconds);
            return Task.FromResult(0);
        }

        private Task OnNewFile(IFileItem file)
        {
            ApiHubFile apiHubFile = new ApiHubFile(file);

            TriggeredFunctionData input = new TriggeredFunctionData
            {
                TriggerValue = apiHubFile
            };
            return _executor.TryExecuteAsync(input, CancellationToken.None);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_poll != null)
            {
                await _poll.StopAsync();
                _poll = null;
            }
        }
    }
}
