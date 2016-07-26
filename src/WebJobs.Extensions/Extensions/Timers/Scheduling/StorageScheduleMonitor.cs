// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// <see cref="ScheduleMonitor"/> that stores schedule information in blob storage.
    /// </summary>
    public class StorageScheduleMonitor : ScheduleMonitor
    {
        private const string HostContainerName = "azure-webjobs-hosts";
        private readonly JobHostConfiguration _hostConfig;
        private readonly JsonSerializer _serializer;
        private CloudBlobDirectory _timerStatusDirectory;
        private TraceWriter _trace;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="hostConfig">The <see cref="JobHostConfiguration"/>.</param>
        /// <param name="trace">The <see cref="TraceWriter"/>.</param>
        public StorageScheduleMonitor(JobHostConfiguration hostConfig, TraceWriter trace)
        {
            _hostConfig = hostConfig;
            _trace = trace;

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };
            _serializer = JsonSerializer.Create(settings);
        }

        /// <summary>
        /// Gets the blob directory where timer statuses will be stored.
        /// </summary>
        internal CloudBlobDirectory TimerStatusDirectory
        {
            get
            {
                // We have to delay create the blob directory since we require the JobHost ID, and that will only
                // be available AFTER the host as been started
                if (_timerStatusDirectory == null)
                {
                    if (string.IsNullOrEmpty(_hostConfig.HostId))
                    {
                        throw new InvalidOperationException("Unable to determine host ID.");
                    }

                    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(_hostConfig.StorageConnectionString);
                    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                    string timerStatusDirectoryPath = string.Format("timers/{0}", _hostConfig.HostId);
                    _timerStatusDirectory = blobClient.GetContainerReference(HostContainerName).GetDirectoryReference(timerStatusDirectoryPath);
                }
                return _timerStatusDirectory;
            }
        }

        /// <inheritdoc/>
        public override async Task<ScheduleStatus> GetStatusAsync(string timerName)
        {
            CloudBlockBlob statusBlob = GetStatusBlobReference(timerName);

            try
            {
                string statusLine = await statusBlob.DownloadTextAsync();
                ScheduleStatus status;
                using (StringReader stringReader = new StringReader(statusLine))
                {
                    status = (ScheduleStatus)_serializer.Deserialize(stringReader, typeof(ScheduleStatus));
                }
                return status;
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null &&
                    exception.RequestInformation.HttpStatusCode == 404)
                {
                    // we haven't recorded a status yet
                    return null;
                }
                throw;
            }
        }

        /// <inheritdoc/>
        public override async Task UpdateStatusAsync(string timerName, ScheduleStatus status)
        {
            string statusLine;
            using (StringWriter stringWriter = new StringWriter())
            {
                _serializer.Serialize(stringWriter, status);
                statusLine = stringWriter.ToString();
            }

            try
            {
                CloudBlockBlob statusBlob = GetStatusBlobReference(timerName);
                await statusBlob.UploadTextAsync(statusLine);
            }
            catch (Exception ex)
            {
                // best effort
                _trace.Error(string.Format("Function '{0}' failed to update the timer trigger status.", timerName), ex);
            }
        }

        private CloudBlockBlob GetStatusBlobReference(string timerName)
        {
            // Path to the status blob is:
            // timers/{hostId}/{timerName}/status
            string blobName = string.Format("{0}/status", timerName);
            return TimerStatusDirectory.GetBlockBlobReference(blobName);
        }
    }
}
