// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Listeners;
using WebJobs.Extensions.Files;
using WebJobs.Extensions.Files.Listener;

namespace Microsoft.Azure.WebJobs.Files.Listeners
{
    internal sealed class FileListener : IListener
    {
        private readonly FileTriggerAttribute _attribute;
        private readonly FileTriggerExecutor _triggerExecutor;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly FilesConfiguration _config;

        private FileSystemWatcher _watcher;
        private bool _disposed;

        public FileListener(FilesConfiguration config, FileTriggerAttribute attribute, FileTriggerExecutor triggerExecutor)
        {
            _config = config;
            _attribute = attribute;
            _triggerExecutor = triggerExecutor;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_watcher != null && _watcher.EnableRaisingEvents)
            {
                throw new InvalidOperationException("The listener has already been started.");
            }

            if (string.IsNullOrEmpty(_config.RootPath) || !Directory.Exists(_config.RootPath))
            {
                throw new InvalidOperationException("FilesConfiguration.RootPath must be set to a valid directory location.");
            }

            string path = Path.GetDirectoryName(_attribute.Path);
            string watchPath = Path.Combine(_config.RootPath, path);

            if (!Directory.Exists(watchPath))
            {
                throw new InvalidOperationException(string.Format("Path '{0}' does not exist.", watchPath));
            }

            _watcher = new FileSystemWatcher
            {
                Path = watchPath,
                NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = _attribute.Filter
            };

            if ((_attribute.ChangeTypes & WatcherChangeTypes.Changed) != 0)
            {
                _watcher.Changed += new FileSystemEventHandler(FileChangeHandler);
            }
            
            if ((_attribute.ChangeTypes & WatcherChangeTypes.Created) != 0)
            {
                _watcher.Created += new FileSystemEventHandler(FileChangeHandler);
            }

            if ((_attribute.ChangeTypes & WatcherChangeTypes.Deleted) != 0)
            {
                _watcher.Deleted += new FileSystemEventHandler(FileChangeHandler);
            }
            
            if ((_attribute.ChangeTypes & WatcherChangeTypes.Renamed) != 0)
            {
                _watcher.Renamed += new RenamedEventHandler(FileChangeHandler);
            }
            
            _watcher.EnableRaisingEvents = true;

            // on startup, process any preexisting files that haven't been processed yet
            ProcessFiles();

            await Task.FromResult<bool>(true);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_watcher == null || !_watcher.EnableRaisingEvents)
            {
                throw new InvalidOperationException("The listener has not yet been started or has already been stopped.");
            }
 
            // Signal ProcessMessage to shut down gracefully
            _cancellationTokenSource.Cancel();

            _watcher.EnableRaisingEvents = false;

            return Task.FromResult<bool>(true);
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Running callers might still be using the cancellation token.
                // Mark it canceled but don't dispose of the source while the callers are running.
                // Otherwise, callers would receive ObjectDisposedException when calling token.Register.
                // For now, rely on finalization to clean up _cancellationTokenSource's wait handle (if allocated).
                _cancellationTokenSource.Cancel();

                if (_watcher != null)
                {
                    _watcher.Dispose();
                    _watcher = null;
                }

                _disposed = true;
            }
        }

        private void ProcessFiles()
        {
            string[] files = Directory.GetFiles(_watcher.Path, _watcher.Filter);

            int batchSize = 5;  // TODO make configurable
            int skip = 0;
            IEnumerable<string> fileBatch = files.Skip(skip).Take(batchSize);
            while (fileBatch.Any())
            {
                List<Task> batchTasks = new List<Task>();
                foreach (string file in fileBatch)
                {
                    string fileName = Path.GetFileName(file);
                    FileSystemEventArgs args = new FileSystemEventArgs(WatcherChangeTypes.Created, _watcher.Path, fileName);
                    batchTasks.Add(HandleFileChange(args));
                }
                Task.WaitAll(batchTasks.ToArray());

                // get the next batch
                skip += batchSize;
                fileBatch = files.Skip(skip).Take(batchSize);
            }
        }

        // Define the event handlers. 
        private void FileChangeHandler(object source, FileSystemEventArgs e)
        {
            HandleFileChange(e).Wait();
        }

        private async Task HandleFileChange(FileSystemEventArgs eventArgs)
        {
            CancellationToken token = _cancellationTokenSource.Token;
            if (!await _triggerExecutor.ExecuteAsync(eventArgs, token))
            {
                token.ThrowIfCancellationRequested();
            }

            if (_attribute.AutoDelete)
            {
                File.Delete(eventArgs.FullPath);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }
    }
}
