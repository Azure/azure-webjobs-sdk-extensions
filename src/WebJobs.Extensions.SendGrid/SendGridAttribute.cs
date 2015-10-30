// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to binds a parameter to a SendGridMessage that will automatically be
    /// sent when the function completes.
    /// </summary>
    /// <remarks>
    /// The method parameter can be of type <see cref="SendGrid.SendGridMessage"/> or a reference
    /// to one ('ref' parameter). When using a reference parameter, you can indicate that the message
    /// should not be sent by setting it to <see langword="null"/> before your job function returns.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class SendGridAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the message "To" field. May include binding parameters.
        /// </summary>
        public string To { get; set; }

        /// <summary>
        /// Gets or sets the message "From" field. May include binding parameters.
        /// <remarks>
        /// The string must include a From address, and can also include an optional DisplayName,
        /// separated by a colon. Example: "orders@acme.net:Order Processor".
        /// </remarks>
        /// </summary>
        public string From { get; set; }

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
