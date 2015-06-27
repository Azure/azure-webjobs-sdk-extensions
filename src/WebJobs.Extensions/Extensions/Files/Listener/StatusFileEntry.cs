using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Listener
{
    /// <summary>
    /// Represents a single status entry in the status file.
    /// </summary>
    internal class StatusFileEntry
    {
        /// <summary>
        /// Gets or sets the current <see cref="ProcessingState"/>
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ProcessingState State { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the entry.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last write to the target file.
        /// </summary>
        public DateTime LastWrite { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="WatcherChangeTypes"/> enumeration value indicating
        /// which type of file operation this entry is for.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public WatcherChangeTypes ChangeType { get; set; }

        /// <summary>
        /// Gets or sets the ID of the instance that created this entry.
        /// </summary>
        public string InstanceId { get; set; }
    }
}
