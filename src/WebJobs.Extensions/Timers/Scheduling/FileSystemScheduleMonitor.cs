using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Timers.Scheduling
{
    /// <summary>
    /// This class is used to monitor and record schedule occurrences. It stores
    /// schedule occurrence info to persistent storage at runtime.
    /// <see cref="TimerTriggerAttribute"/> uses this class to monitor
    /// schedules to avoid missing scheduled executions.
    /// </summary>
    public class FileSystemScheduleMonitor : ScheduleMonitor
    {
        private string _statusFilePath;

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        public FileSystemScheduleMonitor()
        {
            // default to the D:\HOME\DATA directory when running in Azure WebApps
            string home = Environment.GetEnvironmentVariable("HOME");
            string rootPath = string.Empty;
            if (!string.IsNullOrEmpty(home))
            {
                rootPath = Path.Combine(home, "data");
            }
            else
            {
                rootPath = Path.GetTempPath();
            }

            _statusFilePath = Path.Combine(rootPath, @"webjobssdk\timers");
            Directory.CreateDirectory(_statusFilePath);
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
        public override async Task<bool> IsPastDueAsync(string timerName, DateTime now)
        {
            bool isPastDue = false;

            DateTime? recordedNextOccurrence = await GetStatusAsync(timerName);
            if (recordedNextOccurrence != null && now > recordedNextOccurrence)
            {
                isPastDue = true;
            }
            return isPastDue;
        }

        /// <inheritdoc/>
        public override Task UpdateAsync(string timerName, DateTime lastOccurrence, DateTime nextOccurrence)
        {
            JObject record = new JObject
            {
                { "Last", lastOccurrence },
                { "Next", nextOccurrence }
            };
            string status = record.ToString(Formatting.None);

            string statusFileName = GetStatusFileName(timerName);
            try
            {
                File.WriteAllText(statusFileName, status);
            }
            catch
            {
                // best effort
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// Reads the persisted next occurrence from storage.
        /// </summary>
        /// <param name="timerName">The name of the timer.</param>
        /// <returns></returns>
        protected virtual Task<DateTime?> GetStatusAsync(string timerName)
        {
            string statusFilePath = GetStatusFileName(timerName);
            if (!File.Exists(statusFilePath))
            {
                return Task.FromResult<DateTime?>(null);
            }

            string statusLine = File.ReadAllText(statusFilePath);
            JObject status = JObject.Parse(statusLine);
            DateTime? nextOccurrence = (DateTime)status["Next"];

            return Task.FromResult(nextOccurrence);
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
    }
}
