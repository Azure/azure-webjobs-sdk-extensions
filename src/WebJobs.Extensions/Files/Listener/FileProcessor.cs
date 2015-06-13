using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Listener
{
    /// <summary>
    /// Default file processor.
    /// </summary>
    public class FileProcessor
    {
        private FilesConfiguration _config;
        private FileTriggerAttribute _attribute;
        private ITriggeredFunctionExecutor<FileSystemEventArgs> _executor;
        private CancellationTokenSource _cancellationTokenSource;
        private string _filePath;
        private string _instanceId;

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="context">The <see cref="FileProcessorFactoryContext"/></param>
        /// <param name="cancellationTokenSource">The <see cref="CancellationTokenSource"/></param>
        public FileProcessor(FileProcessorFactoryContext context, CancellationTokenSource cancellationTokenSource)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            if (cancellationTokenSource == null)
            {
                throw new ArgumentNullException("cancellationTokenSource");
            }

            _config = context.Config;
            _attribute = context.Attribute;
            _executor = context.Executor;
            _cancellationTokenSource = cancellationTokenSource;

            string attributePath = _attribute.GetNormalizedPath();
            _filePath = Path.Combine(_config.RootPath, attributePath);
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
        /// Gets the current role instance ID. In Azure WebApps, this will be the
        /// WEBSITE_INSTANCE_ID.
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
                        _instanceId = envValue.Substring(0, 10);
                    }
                }
                return _instanceId;
            }
        }

        /// <summary>
        /// Process the file indicated by the specified <see cref="FileSystemEventArgs"/>.
        /// </summary>
        /// <param name="eventArgs">The <see cref="FileSystemEventArgs"/> indicating the file to process.</param>
        /// <returns>A <see cref="Task"/> representing the file process operation.</returns>
        public virtual async Task ProcessFileAsync(FileSystemEventArgs eventArgs)
        {
            string fileToProcess = eventArgs.FullPath;
            if (!BeginProcessing(fileToProcess))
            {
                return;
            }

            TriggeredFunctionData<FileSystemEventArgs> input = new TriggeredFunctionData<FileSystemEventArgs>
            {
                // TODO: set this properly
                ParentId = null,
                TriggerValue = eventArgs
            };
            CancellationToken token = _cancellationTokenSource.Token;
            FunctionResult result = await _executor.TryExecuteAsync(input, token);

            CompleteProcessing(fileToProcess, result);

            if (!result.Succeeded)
            {
                token.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// Begin processing the specified file.
        /// </summary>
        /// <param name="filePath">The file to process.</param>
        /// <returns>True if the file should be processed, false otherwise.</returns>
        public virtual bool BeginProcessing(string filePath)
        {
            if (!ShouldProcessFile(filePath))
            {
                return false;
            }

            return TryCreateStatusFile(filePath);
        }

        private bool TryCreateStatusFile(string filePath)
        {
            Stream stream = null;
            try
            {
                // Attempt to create a companion status file. This is the mechanism for handling
                // multi-instance concurrency. If another process (e.g. another instance of the host
                // WebApp) beats us to it, the following line will throw, indicating that somebody
                // else is processing the file.
                string statusFilePath = GetStatusFile(filePath);
                stream = File.Open(statusFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                using (StreamWriter sw = new StreamWriter(stream))
                {
                    stream = null;
                    sw.WriteLine(string.Format("Processing {0} (Instance: {1})", DateTime.UtcNow.ToString("o"), InstanceId));
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            return true;
        }

        /// <summary>
        /// Complete processing of the specified file.
        /// </summary>
        /// <param name="filePath">The file to complete processing for.</param>
        /// <param name="result">The <see cref="FunctionResult"/> for the invoked function.</param>
        public virtual void CompleteProcessing(string filePath, FunctionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            string statusFilePath = GetStatusFile(filePath);
            if (result.Succeeded)
            {
                File.AppendAllText(statusFilePath, string.Format("Processed {0} (Instance: {1})\r\n", DateTime.UtcNow.ToString("o"), InstanceId));
            }
            else
            {
                // if the function failed, clean up any in progress
                // status file
                TryDelete(statusFilePath);
            }  
        }

        /// <summary>
        /// Perform any required cleanup. This includes deleting processed files (when AutoDelete is
        /// True), as well as discovering and reprocessing any failed files.
        /// </summary>
        public virtual void Cleanup()
        {
            // TODO: Look for any status files that have been around for a while but
            // don't indicate success (e.g. failed job functions), and delete the status
            // files to trigger reprocessing

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
            // If a status file exist for the file, it shouldn't be processed (it's
            // either already processed or being processed)
            string statusFileName = GetStatusFile(filePath);
            return !File.Exists(statusFileName);
        }

        /// <summary>
        /// Clean up any files that have been fully processed
        /// </summary>
        public virtual void CleanupProcessedFiles()
        {
            string[] statusFiles = Directory.GetFiles(_filePath, GetStatusFile("*"));
            foreach (string statusFilePath in statusFiles)
            {
                try
                {
                    // first read the status file to determine if the file
                    // processing is complete
                    string[] lines = File.ReadAllLines(statusFilePath);
                    bool isComplete = lines.Length == 2 && lines[1].StartsWith("Processed", StringComparison.OrdinalIgnoreCase);
                    if (!isComplete)
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
                        TryDelete(filePath);
                    }

                    // then delete the status file
                    TryDelete(statusFilePath);
                }
                catch
                {
                    // ignore any delete failures
                }
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

        internal string GetStatusFile(string file)
        {
            return file + "." + StatusFileExtension;
        }
    }
}
