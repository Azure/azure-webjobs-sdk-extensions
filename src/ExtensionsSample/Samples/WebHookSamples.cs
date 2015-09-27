// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Files;
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

        /// <summary>
        /// Demonstrates binding to a custom POCO Type, including model binding in other
        /// binders to values from that POCO.
        /// </summary>
        public static void HookC(
            [WebHookTrigger] Order order,
            [File(@"{OrderId}_{CustomerName}.txt", FileAccess.Write)] out string output,
            TraceWriter trace)
        {
            output = "Order Received!";
            trace.Info(string.Format("HookC invoked! OrderId: {0}", order.OrderId));
        }

        /// <summary>
        /// Demonstrates binding to <see cref="WebHookContext"/> which enables you to
        /// control the response returned.
        /// </summary>
        public static void HookD([WebHookTrigger] WebHookContext context)
        {
            context.Response = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("Custom Response!")
            };
        }
    }
}
