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
        /// Constructs a new instance
        /// </summary>
        public FileSystemScheduleMonitor() : this(Directory.GetCurrentDirectory())
        {
        }

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        public FileSystemScheduleMonitor(string currentDirectory)
        {
            if (string.IsNullOrEmpty(currentDirectory))
            {
                throw new ArgumentNullException("currentDirectory");
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
                    throw new ArgumentNullException("value");
                }
                if (!Directory.Exists(value))
                {
                    throw new ArgumentException("The specified path does not exist.", "value");
                }
                _statusFilePath = value;
            }
        }

        /// <inheritdoc/>
        public override async Task<bool> IsPastDueAsync(string timerName, DateTime now, TimerSchedule schedule)
        {
            TimeSpan pastDueDuration = await GetPastDueDuration(timerName, now, schedule);
            return pastDueDuration != TimeSpan.Zero;
        }

        /// <inheritdoc/>
        public override Task UpdateAsync(string timerName, DateTime lastOccurrence, DateTime nextOccurrence)
        {
            StatusEntry status = new StatusEntry
            {
                Last = lastOccurrence,
                Next = nextOccurrence
            };

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
        /// Returns the <see cref="TimeSpan"/> duration that the specified timer is past due.
        /// </summary>
        /// <param name="timerName">The name of the timer.</param>
        /// <param name="now">The current time.</param>
        /// <param name="schedule">The <see cref="TimerSchedule"/>.</param>
        /// <returns>The duration the timer is past due.</returns>
        protected async Task<TimeSpan> GetPastDueDuration(string timerName, DateTime now, TimerSchedule schedule)
        {
            StatusEntry status = GetStatus(timerName);
            DateTime recordedNextOccurrence;
            if (status == null)
            {
                // If we've never recorded a status for this timer, write an initial
                // status entry. This ensures that for a new timer, we've captured a
                // status log for the next occurrence even though no occurrence has happened yet
                // (ensuring we don't miss an occurrence)
                DateTime nextOccurrence = schedule.GetNextOccurrence(now);
                await UpdateAsync(timerName, default(DateTime), nextOccurrence);
                recordedNextOccurrence = nextOccurrence;
            }
            else
            {
                // ensure that the schedule hasn't been updated since the last
                // time we checked, and if it has, update the status file
                DateTime expectedNextOccurrence;
                if (status.Last == default(DateTime))
                {
                    // there have been no executions of the function yet, so compute
                    // from now
                    expectedNextOccurrence = schedule.GetNextOccurrence(now);
                }
                else
                {
                    // compute the next occurrence from the last
                    expectedNextOccurrence = schedule.GetNextOccurrence(status.Last);
                }

                if (status.Next != expectedNextOccurrence)
                {
                    await UpdateAsync(timerName, status.Last, expectedNextOccurrence);
                }
                recordedNextOccurrence = status.Next;
            }

            if (now > recordedNextOccurrence)
            {
                // if now is after the last next occurrence we recorded, we know we've missed
                // at least one schedule instance and we are past due
                return now - recordedNextOccurrence;
            }
            else
            {
                // not past due
                return TimeSpan.Zero;
            }   
        }

        /// <summary>
        /// Reads the persisted next occurrence from storage.
        /// </summary>
        /// <param name="timerName">The name of the timer.</param>
        /// <returns></returns>
        protected StatusEntry GetStatus(string timerName)
        {
            string statusFilePath = GetStatusFileName(timerName);
            if (!File.Exists(statusFilePath))
            {
                return null;
            }

            StatusEntry status;
            string statusLine = File.ReadAllText(statusFilePath);
            using (StringReader stringReader = new StringReader(statusLine))
            {
                status = (StatusEntry)_serializer.Deserialize(stringReader, typeof(StatusEntry));
            }

            return status;
        }

        /// <summary>
        /// Returns the schedule status file name for the specified timer
        /// </summary>
        /// <param name="timerName">The timer name</param>
        /// <returns></returns>
        protected internal string GetStatusFileName(string timerName)
        {
            return Path.Combine(StatusFilePath, timerName + ".status");
        }

        /// <summary>
        /// Represents a timer schedule status file entry
        /// </summary>
        protected class StatusEntry
        {
            /// <summary>
            /// The last recorded schedule occurrence
            /// </summary>
            public DateTime Last { get; set; }

            /// <summary>
            /// The expected next schedule occurrence
            /// </summary>
            public DateTime Next { get; set; }
        }
    }
}
