// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Timers;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Extensions.Files.Listener;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Files.Listeners
{
    internal sealed class FileListener : IListener
    {
        private readonly TimeSpan _changeEventDebounceInterval = TimeSpan.FromSeconds(1);

        private readonly FileTriggerAttribute _attribute;
        private readonly ITriggeredFunctionExecutor _triggerExecutor;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IOptions<FilesOptions> _options;
        private readonly string _watchPath;
        private readonly ILogger _logger;
        private readonly IFileProcessorFactory _fileProcessorFactory;
        private ActionBlock<FileSystemEventArgs> _workQueue;
        private FileProcessor _processor;
        private System.Timers.Timer _cleanupTimer;
        private Random _rand = new Random();
        private FileSystemWatcher _watcher;
        private bool _disposed;

        public FileListener(IOptions<FilesOptions> options, FileTriggerAttribute attribute, ITriggeredFunctionExecutor triggerExecutor, ILogger logger, IFileProcessorFactory fileProcessorFactory)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _attribute = attribute ?? throw new ArgumentNullException(nameof(attribute));
            _triggerExecutor = triggerExecutor ?? throw new ArgumentNullException(nameof(triggerExecutor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileProcessorFactory = fileProcessorFactory ?? throw new ArgumentNullException(nameof(fileProcessorFactory));

            _cancellationTokenSource = new CancellationTokenSource();

            if (string.IsNullOrEmpty(_options.Value.RootPath) || !Directory.Exists(_options.Value.RootPath))
            {
                throw new InvalidOperationException(string.Format("Path '{0}' is invalid. FilesConfiguration.RootPath must be set to a valid directory location.", _options.Value.RootPath));
            }

            _watchPath = Path.Combine(_options.Value.RootPath, _attribute.GetRootPath());
        }

        // for testing
        internal FileProcessor Processor
        {
            get
            {
                return _processor;
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_watcher != null && _watcher.EnableRaisingEvents)
            {
                throw new InvalidOperationException("The listener has already been started.");
            }

            CreateFileWatcher();

            FileProcessorFactoryContext context = new FileProcessorFactoryContext(_options.Value, _attribute, _triggerExecutor, _logger);
            _processor = _fileProcessorFactory.CreateFileProcessor(context);

            ExecutionDataflowBlockOptions options = new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = _processor.MaxQueueSize,
                MaxDegreeOfParallelism = _processor.MaxDegreeOfParallelism,
            };
            _workQueue = new ActionBlock<FileSystemEventArgs>(async (e) => await ProcessWorkItem(e), options);

            // on startup, process any preexisting files that haven't been processed yet
            ProcessFiles();

            // Create a timer to cleanup processed files.
            // The timer doesn't auto-reset. It resets itself as files
            // are completed.
            // We start the timer on startup so we have at least one
            // cleanup pass
            _cleanupTimer = new System.Timers.Timer()
            {
                AutoReset = false,
                Interval = _rand.Next(5 * 1000, 8 * 1000)
            };
            _cleanupTimer.Elapsed += OnCleanupTimer;
            _cleanupTimer.Start();

            await Task.FromResult<bool>(true);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_watcher == null || !_watcher.EnableRaisingEvents)
            {
                throw new InvalidOperationException("The listener has not yet been started or has already been stopped.");
            }

            _cancellationTokenSource.Cancel();

            _watcher.EnableRaisingEvents = false;

            _cleanupTimer.Stop();
            _cleanupTimer.Dispose();
            _cleanupTimer = null;

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

                if (_cleanupTimer != null)
                {
                    _cleanupTimer.Stop();
                    _cleanupTimer.Dispose();
                    _cleanupTimer = null;
                }

                _disposed = true;
            }
        }

        private void CreateFileWatcher()
        {
            if ((_attribute.ChangeTypes & WatcherChangeTypes.Changed) != 0 && _attribute.AutoDelete)
            {
                throw new NotSupportedException("Use of AutoDelete is not supported when using change type 'Changed'.");
            }

            if (!Directory.Exists(_watchPath))
            {
                throw new InvalidOperationException(string.Format("Path '{0}' does not exist.", _watchPath));
            }

            _watcher = new FileSystemWatcher
            {
                Path = _watchPath,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = _attribute.Filter
            };

            if ((_attribute.ChangeTypes & WatcherChangeTypes.Created) != 0)
            {
                _watcher.Created += new FileSystemEventHandler(FileChangeHandler);
            }

            if ((_attribute.ChangeTypes & WatcherChangeTypes.Changed) != 0)
            {
                _watcher.Changed += new FileSystemEventHandler(FileChangeHandler);
            }

            if ((_attribute.ChangeTypes & WatcherChangeTypes.Renamed) != 0)
            {
                _watcher.Renamed += new RenamedEventHandler(FileRenameHandler);
            }

            if ((_attribute.ChangeTypes & WatcherChangeTypes.Deleted) != 0)
            {
                _watcher.Deleted += new FileSystemEventHandler(FileChangeHandler);
            }
            _watcher.EnableRaisingEvents = true;
        }

        private void OnCleanupTimer(object sender, ElapsedEventArgs e)
        {
            _processor.Cleanup();
        }

        private void FileChangeHandler(object source, FileSystemEventArgs e)
        {
            if (_processor.IsStatusFile(e.Name))
            {
                // We never want to trigger on our own status files
                return;
            }

            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                // if this is a Change event stemming from a Create operation
                // we'll skip it - we only care about true change events made to
                // existing files, not the one or more Change events that are
                // raised when a new file is created.
                FileInfo fileInfo = new FileInfo(e.FullPath);
                if ((fileInfo.LastWriteTime - fileInfo.CreationTime) < _changeEventDebounceInterval)
                {
                    return;
                }
            }

            // add the item to the work queue
            _workQueue.Post(e);
        }

        /// <summary>
        /// Queues an incoming File for processing if it was renamed and matches the filter.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileRenameHandler(object sender, RenamedEventArgs e)
        {
            if (_processor.IsStatusFile(e.Name))
            {
                // We never want to trigger on our own status files
                return;
            }

            _workQueue.Post(e);
        }

        private async Task ProcessWorkItem(FileSystemEventArgs e)
        {
            await _processor.ProcessFileAsync(e, _cancellationTokenSource.Token);

            // Whenever we finish processing a file, reset the cleanup timer
            // so it will run
            _cleanupTimer.Enabled = true;
        }

        private void ProcessFiles()
        {
            // scan for any files that require processing (either new unprocessed files,
            // or files that have failed previous processing)
            string[] filesToProcess = Directory.GetFiles(_watchPath, _attribute.Filter)
                .Where(p => _processor.ShouldProcessFile(p)).ToArray();

            if (filesToProcess.Length > 0)
            {
                _logger.LogDebug($"Found {filesToProcess.Length} file(s) at path '{_watchPath}' for ready processing");
            }

            foreach (string fileToProcess in filesToProcess)
            {
                WatcherChangeTypes changeType = WatcherChangeTypes.Created;
                string statusFilePath = _processor.GetStatusFile(fileToProcess);

                try
                {
                    StatusFileEntry statusEntry = null;
                    if (_processor.GetLastStatus(statusFilePath, out statusEntry))
                    {
                        // if an in progress status file exists, we determine the ChangeType
                        // from the last entry (incomplete) in the file
                        changeType = statusEntry.ChangeType;
                    }
                }
                catch (IOException)
                {
                    // if we get an exception reading the status file, it's
                    // likely because someone started processing and has it locked
                    continue;
                }

                string fileName = Path.GetFileName(fileToProcess);
                FileSystemEventArgs args = new FileSystemEventArgs(changeType, _watchPath, fileName);
                _workQueue.Post(args);
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
