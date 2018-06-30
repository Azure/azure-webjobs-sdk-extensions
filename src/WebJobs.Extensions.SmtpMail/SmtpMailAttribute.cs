// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Description;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to bind a parameter to a MailMessage that will automatically be
    /// sent when the function completes.
    /// </summary>
    [Binding]
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public sealed class SmtpMailAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the connection string. May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string Connection { get; set; }

        /// <summary>
        /// Gets or sets the message "To" field. May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string To { get; set; }

        /// <summary>
        /// Gets or sets the message "From" field. May include binding parameters.
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

        /// <summary>
        /// Gets or sets the message "Html" field. May include binding parameters.
        /// </summary>
        [AutoResolve]
        public string Html { get; set; }
    }
}
