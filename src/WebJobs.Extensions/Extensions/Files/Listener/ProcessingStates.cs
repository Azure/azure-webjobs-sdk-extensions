namespace Microsoft.Azure.WebJobs.Extensions.Files.Listener
{
    /// <summary>
    /// Enumeration of the possible processing states a
    /// file can be in.
    /// </summary>
    internal enum ProcessingState
    {
        /// <summary>
        /// The file is being processed.
        /// </summary>
        Processing,

        /// <summary>
        /// Processing is complete for the file.
        /// </summary>
        Processed
    }
}
