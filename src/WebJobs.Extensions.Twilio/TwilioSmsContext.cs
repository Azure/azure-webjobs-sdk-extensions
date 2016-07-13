using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twilio;

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
