// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.WebHooks;
using Microsoft.Azure.WebJobs.Host;

namespace ExtensionsSample
{
    public static class WebHookSamples
    {
        /// <summary>
        /// This WebHook uses the default convention based routing, and is invoked
        /// by POST requests to http://localhost:{port}/WebHookSamples/HookA.
        /// </summary>
        public static void HookA([WebHookTrigger] string body, TraceWriter trace)
        {
            trace.Info(string.Format("HookA invoked! Body: {0}", body));
        }

        /// <summary>
        /// This WebHook declares its route, and is invoked by POST requests
        /// to http://localhost:{port}/Sample/HookB.
        /// </summary>
        public static async Task HookB([WebHookTrigger("Sample/HookB")] HttpRequestMessage request, TraceWriter trace)
        {
            string body = await request.Content.ReadAsStringAsync();
            trace.Info(string.Format("HookB invoked! Body: {0}", body));
        }
    }
}
