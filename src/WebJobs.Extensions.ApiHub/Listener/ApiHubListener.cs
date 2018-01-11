// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ApiHub;
using Microsoft.Azure.WebJobs.Extensions.ApiHub.Common;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.ApiHub
{
    // TODO: leveraging singleton listener for now until the framework supports a generalized listener/queue mechanism.
    [Singleton(Mode = SingletonMode.Listener)]
    internal class ApiHubListener : IListener
    {
        private const string HostContainerName = "azure-webjobs-hosts";
        private const string PoisonQueueName = "webjobs-apihubtrigger-poison";
        private const string ApiHubBlobDirectoryPathTemplate = "apihubs/{0}";

        private const int DefaultPollIntervalInSeconds = 90;

        private string _siteName;
        private string _functionName;
        private string _connectionStringSetting;
        private CloudBlobDirectory _apiHubBlobDirectory;
        private CloudQueue _poisonQueue;

        private JobHostConfiguration _config;
        private ApiHubConfiguration _apiHubConfig;
        private IFolderItem _folderSource;
        private ITriggeredFunctionExecutor _executor;

        private IFileWatcher _poll;
        private int _pollIntervalInSeconds;
        private FileWatcherType _fileWatcherType;
        private TraceWriter _trace;
        private JsonSerializer _serializer;

        public ApiHubListener(ApiHubConfiguration apiHubConfig, JobHostConfiguration config, IFolderItem folder, string functionName, ITriggeredFunctionExecutor executor, TraceWriter trace, ApiHubFileTriggerAttribute attribute)
        {
            _apiHubConfig = apiHubConfig;
            _config = config;
            _folderSource = folder;
            _functionName = functionName;
            _executor = executor;
            _trace = trace;
            _pollIntervalInSeconds = attribute.PollIntervalInSeconds;
            _fileWatcherType = attribute.FileWatcherType;
            _siteName = _config.HostId;
            _connectionStringSetting = attribute.ConnectionStringSetting;
            _serializer = JsonSerializer.Create();

            CloudQueueClient queueClient = CloudStorageAccount.Parse(_config.StorageConnectionString).CreateCloudQueueClient();
            _poisonQueue = queueClient.GetQueueReference(PoisonQueueName);
        }

        public void Cancel()
        {
            // nop.. this is fire and forget
            Task taskIgnore = StopAsync(CancellationToken.None);
        }

        public void Dispose()
        {
            // nop
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(this._config.StorageConnectionString);
            CloudBlobClient blobClient = account.CreateCloudBlobClient();

            string apiHubBlobDirectoryPath = string.Format(ApiHubBlobDirectoryPathTemplate, this._siteName);
            _apiHubBlobDirectory = blobClient.GetContainerReference(HostContainerName).GetDirectoryReference(apiHubBlobDirectoryPath);

            // Need to start polling from the point where it was left off. if this is the first time then lastPoll will be null.
            var lastPoll = await GetLastPollStatusAsync();

            Uri nextUri = null;

            if (lastPoll != null)
            {
                // This is to make sure the trigger connection or path is not updated after the initial setup and if so reset the polling point.
                if ((lastPoll.Connection == null || string.Compare(lastPoll.Connection, _connectionStringSetting, true) == 0) &&
                    (lastPoll.FilePath == null || string.Compare(lastPoll.FilePath, _folderSource.Path, true) == 0))
                {
                    Uri.TryCreate(lastPoll.PollUrl, UriKind.Absolute, out nextUri);
                }
            }

            _poll = _folderSource.CreateFileWatcher(_fileWatcherType, OnFileWatcher, nextItem: nextUri, pollIntervalInSeconds: _pollIntervalInSeconds > 0 ? _pollIntervalInSeconds : DefaultPollIntervalInSeconds);

            if (_poll == null)
            {
                var errorText = string.Format("Path '{0}' is invalid. Path must be set to a valid folder location.", _folderSource.Path);
                _trace.Error(errorText);

                throw new InvalidOperationException(errorText);
            }
        }

        private async Task OnFileWatcher(IFileItem file, object obj)
        {
            ApiHubFile apiHubFile = new ApiHubFile(file);

            TriggeredFunctionData input = new TriggeredFunctionData
            {
                TriggerValue = apiHubFile
            };

            var functionResult = await _executor.TryExecuteAsync(input, CancellationToken.None);

            string pollStatus = null;
            var uri = obj as Uri;

            if (uri != null)
            {
                pollStatus = uri.AbsoluteUri;
            }
            else
            {
                pollStatus = obj.ToString();
            }

            if (functionResult.Succeeded)
            {
                // If function successfully completes then the next poll Uri will be logged in an Azure Blob.
                var status = new ApiHubStatus
                {
                    PollUrl = pollStatus,
                    FilePath = Path.GetDirectoryName(apiHubFile.Path),
                    Connection = this._connectionStringSetting
                };

                await SetNextPollStatusAsync(status);
            }
            else
            {
                var status = await GetLastPollStatusAsync();

                if (status == null)
                {
                    status = new ApiHubStatus
                    {
                        FilePath = Path.GetDirectoryName(apiHubFile.Path),
                        Connection = this._connectionStringSetting
                    };
                }

                if (status.RetryCount < this._apiHubConfig.MaxFunctionExecutionRetryCount)
                {
                    status.RetryCount++;
                    _trace.Error($"Function {_functionName} failed to successfully process file {apiHubFile.Path}. Number of retries: {status.RetryCount}.");

                    await SetNextPollStatusAsync(status);
                    await OnFileWatcher(file, obj);
                }
                else
                {
                    // The maximum retries for the function execution has reached. The file info will be added to the poison queue and the next poll status will be updated to skip the file.
                    status.PollUrl = pollStatus;
                    status.RetryCount = 0;
                    _trace.Error($"Function {_functionName} failed to successfully process file {apiHubFile.Path} after max allowed retries of {_apiHubConfig.MaxFunctionExecutionRetryCount}. The file info will be moved to queue {PoisonQueueName}.");

                    await MoveToPoisonQueueAsync(apiHubFile);
                    await SetNextPollStatusAsync(status);
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_poll != null)
            {
                await _poll.StopAsync();
                _poll = null;
            }
        }

        private async Task MoveToPoisonQueueAsync(ApiHubFile apiHubFile)
        {
            await this._poisonQueue.CreateIfNotExistsAsync();

            var fileStatus = new ApiHubFileInfo
            {
                FilePath = apiHubFile.Path,
                FunctionName = _functionName,
                Connection = _connectionStringSetting
            };

            string content;
            using (StringWriter stringWriter = new StringWriter())
            {
                _serializer.Serialize(stringWriter, fileStatus);
                content = stringWriter.ToString();
            }

            var queueMessage = new CloudQueueMessage(content);

            await _poisonQueue.AddMessageAsync(queueMessage);
        }

        private async Task<ApiHubStatus> GetLastPollStatusAsync()
        {
            ApiHubStatus status;
            try
            {
                var apiHubBlob = GetBlobReference();

                var statusLine = await apiHubBlob.DownloadTextAsync();
                using (StringReader stringReader = new StringReader(statusLine))
                {
                    status = (ApiHubStatus)_serializer.Deserialize(stringReader, typeof(ApiHubStatus));
                }
                
            }
            catch (StorageException e)
            {
                if (e.RequestInformation == null ||
                    e.RequestInformation.HttpStatusCode != 404)
                {
                    _trace.Error($"Encountered a storage exception while getting the next poll status from the blob storage. Path: {_folderSource.Path}, Message: {e.Message} ");
                }
                status = null;
            }
            catch (Exception e)
            {
                _trace.Error($"Encountered an exception while getting the next poll status from the blob storage. Path: {_folderSource.Path}, Message: {e.Message} ");
                status = null;
            }

            return status;
        }

        private async Task SetNextPollStatusAsync(ApiHubStatus status)
        {
            try
            {
                var apiHubBlob = GetBlobReference();

                string statusLine;
                using (StringWriter stringWriter = new StringWriter())
                {
                    _serializer.Serialize(stringWriter, status);
                    statusLine = stringWriter.ToString();
                }

                await apiHubBlob.UploadTextAsync(statusLine);
            }
            catch (Exception e)
            {
                _trace.Error($"Encountered an exception while setting the next poll status to the blob storage. Path: {_folderSource.Path}, Message: {e.Message}");
            }
        }

        private CloudBlockBlob GetBlobReference()
        {
            // Path to apiHub blob is:
            // apihubs/{siteName}/{functionName}/status
            string blobName = $"{_functionName}/status";
            return this._apiHubBlobDirectory.GetBlockBlobReference(blobName);
        }

        private class ApiHubStatus
        {
            public string PollUrl { get; set; }

            public string FilePath { get; set; }

            public string Connection { get; set; }

            public int RetryCount { get; set; }
        }

        private class ApiHubFileInfo
        {
            public string FunctionName { get; set; }

            public string FilePath { get; set; }

            public string Connection { get; set; }
        }
    }
}
