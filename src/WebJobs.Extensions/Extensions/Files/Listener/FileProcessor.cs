// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Executors;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Listener
{
    /// <summary>
    /// Default file processor.
    /// </summary>
    public class FileProcessor
    {
        private readonly FilesConfiguration _config;
        private readonly FileTriggerAttribute _attribute;
        private readonly TraceWriter _trace;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly string _filePath;
        private readonly JsonSerializer _serializer;
        private string _instanceId;
 
        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="context">The <see cref="FileProcessorFactoryContext"/></param>
        public FileProcessor(FileProcessorFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            _config = context.Config;
            _attribute = context.Attribute;
            _executor = context.Executor;
            _trace = context.Trace;

            string attributePath = _attribute.GetNormalizedPath();
            _filePath = Path.Combine(_config.RootPath, attributePath);

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
            };
            _serializer = JsonSerializer.Create(settings);
        }

        /// <summary>
        /// Gets the file extension that will be used for the status files
        /// that are created for processed files.
        /// </summary>
        public virtual string StatusFileExtension
        {
            get
            {
                return "status";
            }
        }

        /// <summary>
        /// Gets or sets the maximum degree of parallelism that will be used
        /// when processing files concurrently.
        /// </summary>
        /// <remarks>
        /// Files are added to an internal processing queue as file events
        /// are detected, and they're processed in parallel based on this setting.
        /// </remarks>
        public virtual int MaxDegreeOfParallelism 
        { 
            get
            {
                return 5;
            }
        }

        /// <summary>
        /// Gets or sets the bounds on the maximum number of files that
        /// can be queued up for processing at one time. When set to -1,
        /// the work queue is unbounded.
        /// </summary>
        public virtual int MaxQueueSize 
        { 
            get
            {
                return -1;
            }
        }

        /// <summary>
        /// Gets the current role instance ID. In Azure WebApps, this will be the
        /// WEBSITE_INSTANCE_ID. In non Azure scenarios, this will default to the
        /// Process ID.
        /// </summary>
        public virtual string InstanceId
        {
            get
            {
                if (_instanceId == null)
                {
                    string envValue = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
                    if (!string.IsNullOrEmpty(envValue))
                    {
                        _instanceId = envValue.Substring(0, 20);
                    }
                    else
                    {
                        _instanceId = Process.GetCurrentProcess().Id.ToString();
                    }
                }
                return _instanceId;
            }
        }

        /// <summary>
        /// Process the file indicated by the specified <see cref="FileSystemEventArgs"/>.
        /// </summary>
        /// <param name="eventArgs">The <see cref="FileSystemEventArgs"/> indicating the file to process.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
        /// <returns>
        /// A <see cref="Task"/> that returns true if the file was processed successfully, false otherwise.
        /// </returns>
        public virtual async Task<bool> ProcessFileAsync(FileSystemEventArgs eventArgs, CancellationToken cancellationToken)
        {
            try
            {
                string filePath = eventArgs.FullPath;
                using (StreamWriter statusWriter = AquireStatusFileLock(filePath, eventArgs.ChangeType))
                {
                    if (statusWriter == null)
                    {
                        return false;
                    }

                    // write an entry indicating the file is being processed
                    StatusFileEntry status = new StatusFileEntry
                    {
                        State = ProcessingState.Processing,
                        Timestamp = DateTime.UtcNow,
                        LastWrite = File.GetLastWriteTimeUtc(filePath),
                        ChangeType = eventArgs.ChangeType,
                        InstanceId = InstanceId
                    };
                    _serializer.Serialize(statusWriter, status);
                    statusWriter.WriteLine();

                    // invoke the job function
                    TriggeredFunctionData input = new TriggeredFunctionData
                    {
                        TriggerValue = eventArgs
                    };
                    FunctionResult result = await _executor.TryExecuteAsync(input, cancellationToken);

                    if (result.Succeeded)
                    {
                        // write a status entry indicating processing is complete
                        status.State = ProcessingState.Processed;
                        status.Timestamp = DateTime.UtcNow;
                        _serializer.Serialize(statusWriter, status);
                        statusWriter.WriteLine();
                        return true;
                    }
                    else
                    {         
                        // If the function failed, we leave the in progress status
                        // file as is (it will show "Processing"). The file will be
                        // reprocessed later on a clean-up pass.
                        statusWriter.Close();
                        cancellationToken.ThrowIfCancellationRequested();
                        return false;
                    }
                }             
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Perform any required cleanup. This includes deleting processed files (when AutoDelete is
        /// True), as well as discovering and reprocessing any failed files.
        /// </summary>
        public virtual void Cleanup()
        {
            if (_attribute.AutoDelete)
            {
                CleanupProcessedFiles();
            }
        }

        /// <summary>
        /// Determines whether the specified file should be processed.
        /// </summary>
        /// <param name="filePath">The candidate file for processing.</param>
        /// <returns>True if the file should be processed, false otherwise.</returns>
        public virtual bool ShouldProcessFile(string filePath)
        {
            string statusFilePath = GetStatusFile(filePath);
            if (!File.Exists(statusFilePath))
            {
                return true;
            }

            StatusFileEntry statusEntry = GetLastStatus(statusFilePath);
            return statusEntry == null || statusEntry.State != ProcessingState.Processed;
        }

        internal StreamWriter AquireStatusFileLock(string filePath, WatcherChangeTypes changeType)
        {
            Stream stream = null;
            try
            {
                // Attempt to create (or update) the companion status file and lock it. The status
                // file is the mechanism for handling multi-instance concurrency.
                string statusFilePath = GetStatusFile(filePath);
                stream = File.Open(statusFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                // Once we've established the lock, we need to check to ensure that another instance
                // hasn't already processed the file in the time between our getting the event and
                // aquiring the lock.
                StatusFileEntry statusEntry = GetLastStatus(stream);
                if (statusEntry != null && statusEntry.State == ProcessingState.Processed)
                {
                    // For file Create, we have no additional checks to perform. However for
                    // file Change, we need to also check the LastWrite value for the entry
                    // since there can be multiple Processed entries in the file over time.
                    if (changeType == WatcherChangeTypes.Created)
                    {
                        return null;
                    }
                    else if (changeType == WatcherChangeTypes.Changed &&
                        File.GetLastWriteTimeUtc(filePath) == statusEntry.LastWrite)
                    {
                        return null;
                    }
                }
                
                stream.Seek(0, SeekOrigin.End);
                StreamWriter streamReader = new StreamWriter(stream);
                streamReader.AutoFlush = true;
                stream = null;

                return streamReader;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }
        }

        internal StatusFileEntry GetLastStatus(string statusFilePath)
        {
            using (Stream stream = File.OpenRead(statusFilePath))
            {
                return GetLastStatus(stream);
            }
        }

        internal StatusFileEntry GetLastStatus(Stream statusFileStream)
        {
            StatusFileEntry statusEntry = null;

            using (StreamReader reader = new StreamReader(statusFileStream, Encoding.UTF8, false, 1024, true))
            {
                string text = reader.ReadToEnd();
                string[] fileLines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                string lastLine = fileLines.LastOrDefault();
                if (!string.IsNullOrEmpty(lastLine))
                {
                    using (StringReader stringReader = new StringReader(lastLine))
                    {
                        statusEntry = (StatusFileEntry)_serializer.Deserialize(stringReader, typeof(StatusFileEntry));
                    }
                }
            }

            statusFileStream.Seek(0, SeekOrigin.End);

            return statusEntry;
        }

        internal string GetStatusFile(string file)
        {
            return file + "." + StatusFileExtension;
        }

        /// <summary>
        /// Clean up any files that have been fully processed
        /// </summary>
        public virtual void CleanupProcessedFiles()
        {
            int filesDeleted = 0;
            string[] statusFiles = Directory.GetFiles(_filePath, GetStatusFile("*"));
            foreach (string statusFilePath in statusFiles)
            {
                try
                {
                    // verify that the file has been fully processed
                    StatusFileEntry statusEntry = GetLastStatus(statusFilePath);
                    if (statusEntry.State != ProcessingState.Processed)
                    {
                        continue;
                    }

                    // get all files starting with that file name. For example, for
                    // status file input.dat.status, this might return input.dat and
                    // input.dat.meta (if the file has other companion files)
                    string targetFileName = Path.GetFileNameWithoutExtension(statusFilePath);
                    string[] files = Directory.GetFiles(_filePath, targetFileName + "*");

                    // first delete the non status file(s)
                    foreach (string filePath in files)
                    {
                        if (Path.GetExtension(filePath).TrimStart('.') == StatusFileExtension)
                        {
                            continue;
                        }
                        if (TryDelete(filePath))
                        {
                            filesDeleted++;
                        }
                    }

                    // then delete the status file
                    if (TryDelete(statusFilePath))
                    {
                        filesDeleted++;
                    }
                }
                catch
                {
                    // ignore any delete failures
                }
            }

            if (filesDeleted > 0)
            {
                _trace.Verbose(string.Format("File Cleanup ({0}): {1} files deleted", _filePath, filesDeleted));
            }
        }

        private static bool TryDelete(string filePath)
        {
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
