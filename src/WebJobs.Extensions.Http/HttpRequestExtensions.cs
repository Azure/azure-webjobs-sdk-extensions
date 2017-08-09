using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Microsoft.Azure.WebJobs.Extensions
{
    public static class HttpRequestExtensions
    {
        private const int DefaultBufferSize = 1024;

        public static async Task<string> ReadAsStringAsync(this HttpRequest request)
        {
            request.EnableRewind();

            string result = null;
            using (var reader = new StreamReader(
                request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: DefaultBufferSize,
                leaveOpen: true))
            {
                result = await reader.ReadToEndAsync();
            }

            request.Body.Seek(0, SeekOrigin.Begin);

            return result;
        }

        public static IDictionary<string, string> GetQueryParameterDictionary(this HttpRequest request)
        {
            // last one wins for any duplicate query parameters
            return request.Query.GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(p => p.Key, s => s.Last().Value.ToString(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
