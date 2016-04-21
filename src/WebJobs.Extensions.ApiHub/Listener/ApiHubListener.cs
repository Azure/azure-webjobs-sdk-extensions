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
    // TODO: leveraging singleton listener for now untill the framework supports a generalized listener/queue mechanism.
    [Singleton(Mode = SingletonMode.Listener)]
    internal class ApiHubListener : IListener
    {
        internal IFolderItem _folderSource;
        internal ITriggeredFunctionExecutor _executor;

        private const int DefaultPollIntervalInSeconds = 90;

        private IFileWatcher _poll;
        private int _pollIntervalInSeconds;
        private FileWatcherType _fileWatcherType;

        public ApiHubListener(
            IFolderItem folder,
            ITriggeredFunctionExecutor executor,
            int pollIntervalInSeconds,
            FileWatcherType fileWatcherType = FileWatcherType.Created)
        {
            this._folderSource = folder;
            this._executor = executor;
            this._pollIntervalInSeconds = pollIntervalInSeconds;
            this._fileWatcherType = fileWatcherType;
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
            _poll = _folderSource.CreateFileWatcher(_fileWatcherType, OnFileWatcher, nextItem: null, pollIntervalInSeconds: _pollIntervalInSeconds > 0 ? _pollIntervalInSeconds : DefaultPollIntervalInSeconds);

            if (_poll == null)
            {
                throw new InvalidOperationException(string.Format("Path '{0}' is invalid. IFolderItem.RootPath must be set to a valid directory location.", _folderSource.Path));
            }

            // TODO: need to decide what to do when _poll is null i.e. trigger folder does not exist.
            return Task.FromResult(0);
        }

        private Task OnFileWatcher(IFileItem file, object obj)
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
