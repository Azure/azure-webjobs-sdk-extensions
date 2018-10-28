// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// This class is used to monitor and record schedule occurrences. It stores
    /// schedule occurrence info to the file system at runtime.
    /// <see cref="TimerTriggerAttribute"/> uses this class to monitor
    /// schedules to avoid missing scheduled executions.
    /// </summary>
    public class FileSystemScheduleMonitor : ScheduleMonitor
    {
        private readonly JsonSerializer _serializer;
        private string _statusFilePath;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public FileSystemScheduleMonitor() : this(Directory.GetCurrentDirectory())
        {
        }

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public FileSystemScheduleMonitor(string currentDirectory)
        {
            if (string.IsNullOrEmpty(currentDirectory))
            {
                throw new ArgumentNullException(nameof(currentDirectory));
            }

            // default to the D:\HOME\DATA directory when running in Azure WebApps
            string home = Environment.GetEnvironmentVariable("HOME");
            string rootPath = string.Empty;
            if (!string.IsNullOrEmpty(home))
            {
                rootPath = Path.Combine(home, "data");

                // Determine the path to the WebJobs folder, so we can write our status
                // files there. We leverage the fact that the TEMP directory structure we
                // run from is the same as the data directory structure
                int start = currentDirectory.IndexOf("jobs", StringComparison.OrdinalIgnoreCase);
                int end = currentDirectory.LastIndexOf(Path.DirectorySeparatorChar);
                if (start > 0 && end > 0)
                {
                    string jobPath = currentDirectory.Substring(start, end - start);
                    _statusFilePath = Path.Combine(rootPath, jobPath);
                }
            }
            else
            {
                rootPath = Path.GetTempPath();
            }

            if (string.IsNullOrEmpty(_statusFilePath) || !Directory.Exists(_statusFilePath))
            {
                _statusFilePath = Path.Combine(rootPath, @"webjobs\timers");
            }
            Directory.CreateDirectory(_statusFilePath);

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };
            _serializer = JsonSerializer.Create(settings);
        }

        /// <summary>
        /// Gets or sets the path where schedule status files will be written.
        /// </summary>
        public string StatusFilePath
        {
            get
            {
                return _statusFilePath;
            }

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(nameof(value));
                }
                if (!Directory.Exists(value))
                {
                    throw new ArgumentException("The specified path does not exist.", nameof(value));
                }
                _statusFilePath = value;
            }
        }

        /// <inheritdoc/>
        public override Task<ScheduleStatus> GetStatusAsync(string timerName)
        {
            string statusFilePath = GetStatusFileName(timerName);
            if (!File.Exists(statusFilePath))
            {
                return Task.FromResult<ScheduleStatus>(null);
            }

            ScheduleStatus status;
            string statusLine = File.ReadAllText(statusFilePath);
            using (StringReader stringReader = new StringReader(statusLine))
            {
                status = (ScheduleStatus)_serializer.Deserialize(stringReader, typeof(ScheduleStatus));
            }

            return Task.FromResult(status);
        }

        /// <inheritdoc/>
        public override Task UpdateStatusAsync(string timerName, ScheduleStatus status)
        {
            string statusLine;
            using (StringWriter stringWriter = new StringWriter())
            {
                _serializer.Serialize(stringWriter, status);
                statusLine = stringWriter.ToString();
            }

            string statusFileName = GetStatusFileName(timerName);
            try
            {
                File.WriteAllText(statusFileName, statusLine);
            }
            catch
            {
                // best effort
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// Returns the schedule status file name for the specified timer.
        /// </summary>
        /// <param name="timerName">The timer name.</param>
        /// <returns></returns>
        protected internal string GetStatusFileName(string timerName)
        {
            return Path.Combine(StatusFilePath, timerName + ".status");
        }
    }
}
