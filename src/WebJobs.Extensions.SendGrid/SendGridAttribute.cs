// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind a parameter to a SendGridMessage that will automatically be
    /// sent when the function completes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SendGridAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the message "To" field. May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string To { get; set; }

        /// <summary>
        /// Gets or sets the message "From" field. May include binding parameters.
        /// <remarks>
        /// The string must include a From address, and can also include an optional DisplayName,
        /// separated by a colon. Example: "orders@acme.net:Order Processor".
        /// </remarks>
        /// </summary>
        [AutoResolve]
        public string From { get; set; }

        /// <summary>
        /// Gets or sets the message "Subject" field. May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets the message "Text" field. May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string Text { get; set; }
    }
}
