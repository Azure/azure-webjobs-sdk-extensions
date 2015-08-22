using System;

namespace Microsoft.Azure.WebJobs.Extensions
{
    /// <summary>
    /// Binds a function parameter to a SendGridMessage that will automatically be
    /// sent when the function completes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SendGridAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the message "To" field. May include binding parameters.
        /// </summary>
        public string To { get; set; }

        /// <summary>
        /// Gets or sets the message "Subject" field. May include binding parameters.
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the message "Text" field. May include binding parameters.
        /// </summary>
        public string Text { get; set; }
    }
}
