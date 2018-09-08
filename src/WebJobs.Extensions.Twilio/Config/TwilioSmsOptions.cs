// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Twilio
{
    public class TwilioSmsOptions
    {
        public string AccountSid { get; set; }

        public string AuthToken { get; set; }

        public string Body { get; set; }

        public string From { get; set; }

        public string To { get; set; }
    }
}
