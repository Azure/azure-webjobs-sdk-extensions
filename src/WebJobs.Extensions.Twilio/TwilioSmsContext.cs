// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Twilio.Clients;

namespace Microsoft.Azure.WebJobs.Extensions
{
    public class TwilioSmsContext
    {
        public TwilioRestClient Client { get; set; }

        public string From { get; set; }

        public string To { get; set; }

        public string Body { get; set; }
    }
}
