using System;
using System.Configuration;
using System.Net.Mail;

namespace Microsoft.Azure.WebJobs.Extensions
{
    /// <summary>
    /// Defines the configuration options for the SendGrid binding.
    /// </summary>
    public class SendGridConfiguration
    {
        internal const string AzureWebJobsSendGridApiKeyName = "AzureWebJobsSendGridApiKey";

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public SendGridConfiguration()
        {
            ApiKey = ConfigurationManager.AppSettings.Get(AzureWebJobsSendGridApiKeyName);
            if (string.IsNullOrEmpty(ApiKey))
            {
                ApiKey = Environment.GetEnvironmentVariable(AzureWebJobsSendGridApiKeyName);
            }
        }

        /// <summary>
        /// Gets or sets the SendGrid ApiKey. If not explicitly set, the value will be defaulted
        /// to the value specified via the 'AzureWebJobsSendGridApiKey' app setting or the
        /// 'AzureWebJobsSendGridApiKey' environment variable.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the default "to" address that will be used for messages.
        /// This value can be overridden by job functions.
        /// </summary>
        /// <remarks>
        /// An example of when it would be useful to provide a default value for 'to' 
        /// would be for emailing your own admin account to notify you when particular
        /// jobs are executed. In this case, job functions can specify minmal info in
        /// their bindings, for example just a Subject and Text body.
        /// </remarks>
        public string ToAddress { get; set; }

        /// <summary>
        /// Gets or sets the default "from" address that will be used for messages.
        /// This value can be overridden by job functions.
        /// </summary>
        public MailAddress FromAddress { get; set; }
    }
}
