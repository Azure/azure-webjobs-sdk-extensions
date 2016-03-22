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
    class ApiHubListener : IListener
    {
        internal IFolderItem _folderSource;
        internal ITriggeredFunctionExecutor _executor;

        private IFileWatcher _poll;

        public ApiHubListener(
            IFolderItem folder,
            ITriggeredFunctionExecutor executor)
        {
            this._folderSource = folder;
            this._executor = executor;
        }

        public void Cancel()
        {
            // nop
            Task tIgnore = this.StopAsync(CancellationToken.None);
        }

        public void Dispose()
        {
            // nop
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _poll = _folderSource.CreateNewFileWatcher(OnNewFile, 5);
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
