// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    // TODO: leveraging singleton listener for now untill the framework supports a generalized listener/queue mechanism.
    [Singleton(Mode = SingletonMode.Listener)]
    internal class ApiHubListener : IListener
    {
        private IFolderItem _folderSource;
        private ITriggeredFunctionExecutor _executor;

        private const int DefaultPollIntervalInSeconds = 90;

        private IFileWatcher _poll;
        private int _pollIntervalInSeconds;
        private FileWatcherType _fileWatcherType;
        private TraceWriter _trace;

        public ApiHubListener(
            IFolderItem folder,
            ITriggeredFunctionExecutor executor,
            TraceWriter trace,
            int pollIntervalInSeconds,
            FileWatcherType fileWatcherType = FileWatcherType.Created)
        {
            this._folderSource = folder;
            this._executor = executor;
            this._trace = trace;
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
                var errorText = string.Format("Path '{0}' is invalid. IFolderItem.RootPath must be set to a valid directory location.", _folderSource.Path);
                _trace.Error(errorText);

                throw new InvalidOperationException(errorText);
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
