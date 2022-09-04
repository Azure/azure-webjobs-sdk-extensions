// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net.Http;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    /// <summary>
    /// Constants used by the http extension.
    /// </summary>
    public static class HttpExtensionConstants
    {
        /// <summary>
        /// The default route prefix.
        /// </summary>
        public const string DefaultRoutePrefix = "api";

        /// <summary>
        /// Key used for storing route data in <see cref="HttpRequestMessage.Properties"/>.
        /// </summary>
        public const string AzureWebJobsHttpRouteDataKey = "MS_AzureWebJobs_HttpRouteData";

        /// <summary>
        /// Key used for storing WebHook payload data in <see cref="HttpRequestMessage.Properties"/>.
        /// </summary>
        public const string AzureWebJobsWebHookDataKey = "MS_AzureWebJobs_WebHookData";

        /// <summary>
        /// Key used to have WebJobsRouter match against Function routes first then Proxies.
        /// </summary>
        public const string AzureWebJobsUseReverseRoutesKey = "MS_AzureWebJobs_UseReverseRoutes";

        /// <summary>
        /// Key used to set the function name on a route.
        /// </summary>
        public const string FunctionNameRouteTokenKey = "AZUREWEBJOBS_FUNCTIONNAME";
    }
}
