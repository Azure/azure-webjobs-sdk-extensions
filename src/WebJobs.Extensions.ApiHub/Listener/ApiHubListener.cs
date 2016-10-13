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

        public ApiHubListener(
            ApiHubConfiguration apiHubConfig,
            JobHostConfiguration config,
            IFolderItem folder,
            string functionName,
            ITriggeredFunctionExecutor executor,
            TraceWriter trace,
            ApiHubFileTriggerAttribute attribute)
        {
            this._apiHubConfig = apiHubConfig;
            this._config = config;
            this._folderSource = folder;
            this._functionName = functionName;
            this._executor = executor;
            this._trace = trace;
            this._pollIntervalInSeconds = attribute.PollIntervalInSeconds;
            this._fileWatcherType = attribute.FileWatcherType;
            this._siteName = this._config.HostId;
            this._connectionStringSetting = attribute.ConnectionStringSetting;
            _serializer = JsonSerializer.Create();

            CloudQueueClient queueClient = CloudStorageAccount.Parse(this._config.StorageConnectionString).CreateCloudQueueClient();
            this._poisonQueue = queueClient.GetQueueReference(PoisonQueueName);
        }

        public void Cancel()
        {
            // nop.. this is fire and forget
            Task taskIgnore = this.StopAsync(CancellationToken.None);
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
            this._apiHubBlobDirectory = blobClient.GetContainerReference(HostContainerName).GetDirectoryReference(apiHubBlobDirectoryPath);

            // Need to start polling from the point where it was left off. if this is the first time then lastPoll will be null.
            var lastPoll = await GetLastPollStatusAsync();

            Uri nextUri = null;

            if (lastPoll != null)
            {
                // This is to make sure the trigger connection or path is not updated after the initial setup and if so reset the polling point.
                if ((lastPoll.Connection == null || string.Compare(lastPoll.Connection, this._connectionStringSetting, true) == 0) &&
                    (lastPoll.FilePath == null || string.Compare(lastPoll.FilePath, this._folderSource.Path, true) == 0))
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
                    _trace.Error(string.Format("Function {0} failed to successfully process file {1}. Number of retries: {2}. ", _functionName, apiHubFile.Path, status.RetryCount));

                    await SetNextPollStatusAsync(status);
                    await OnFileWatcher(file, obj);
                }
                else
                {
                    // The maximum retries for the function execution has reached. The file info will be added to the poison queue and the next poll status will be updated to skip the file.
                    status.PollUrl = pollStatus;
                    status.RetryCount = 0;
                    _trace.Error(string.Format("Function {0} failed to successfully process file {1} after max allowed retries of {2}. The file info will be moved to queue {3}.", _functionName, apiHubFile.Path, this._apiHubConfig.MaxFunctionExecutionRetryCount, PoisonQueueName));

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
                FunctionName = this._functionName,
                Connection = this._connectionStringSetting
            };

            string content;
            using (StringWriter stringWriter = new StringWriter())
            {
                _serializer.Serialize(stringWriter, fileStatus);
                content = stringWriter.ToString();
            }

            var queueMessage = new CloudQueueMessage(content);

            await this._poisonQueue.AddMessageAsync(queueMessage);
        }

        private async Task<ApiHubStatus> GetLastPollStatusAsync()
        {
            try
            {
                var apiHubBlob = GetBlobReference();

                var statusLine = await apiHubBlob.DownloadTextAsync();
                ApiHubStatus status;
                using (StringReader stringReader = new StringReader(statusLine))
                {
                    status = (ApiHubStatus)_serializer.Deserialize(stringReader, typeof(ApiHubStatus));
                }
                return status;
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation == null ||
                    exception.RequestInformation.HttpStatusCode != 404)
                {
                    var errorText = string.Format("Encountered a storage exception while getting the next poll status from the blob storage. Path: {0}, Message: {1} ", this._folderSource.Path, exception.Message);
                    _trace.Error(errorText);
                }
                return null;
            }
            catch (Exception e)
            {
                var errorText = string.Format("Encountered an exception while getting the next poll status from the blob storage. Path: {0}, Message: {1} ", this._folderSource.Path, e.Message);
                _trace.Error(errorText);
                return null;
            }
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
                var errorText = string.Format("Encountered an exception while setting the next poll status to the blob storage. Path: {0}, Message: {1} ", this._folderSource.Path, e.Message);
                _trace.Error(errorText);
            }
        }

        private CloudBlockBlob GetBlobReference()
        {
            // Path to apiHub blob is:
            // apihubs/{siteName}/{functionName}/status
            string blobName = string.Format("{0}/status", this._functionName);
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
            public string FunctionName { get;  set; }

            public string FilePath { get; set; }

            public string Connection { get; set; }
        }
    }
}
