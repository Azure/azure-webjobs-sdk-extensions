// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal static class DocumentDBUtility
    {
        /// <summary>
        /// Execute the function with retries on throttle.
        /// </summary>
        /// <typeparam name="T">The type of return value from the execution.</typeparam>
        /// <param name="function">The function to execute.</param>
        /// <param name="maxRetries">The maximum number of times to retry if the request is throttled.</param>
        /// <param name="codesToIgnore">Status codes, other than 429, to ignore. If one of these status codes is returned, the method will return the default value of T.</param>
        /// <returns>The response from the execution.</returns>
        // Taken from: https://github.com/ryancrawcour/azure-documentdb-dotnet/blob/e7dd2f685554b0a9def63c8925f8e3ef2ad3bff8/samples/code-samples/Shared/Util/DocumentClientHelper.cs
        public static async Task<T> RetryAsync<T>(Func<Task<T>> function, int maxRetries, params HttpStatusCode[] codesToIgnore)
        {
            TimeSpan sleepTime = TimeSpan.Zero;
            int retriesRemaining = maxRetries;

            while (true)
            {
                try
                {
                    return await function();
                }
                catch (Exception ex)
                {
                    DocumentClientException de = null;

                    if (TryGetDocumentClientException(ex, out de))
                    {
                        if (de.StatusCode.HasValue &&
                            codesToIgnore != null &&
                            (int)de.StatusCode.Value != 429 &&
                            codesToIgnore.Contains(de.StatusCode.Value))
                        {
                            // exit the while loop and return the default value
                            break;
                        }

                        if ((int)de.StatusCode != 429 ||
                            retriesRemaining-- <= 0)
                        {
                            throw;
                        }

                        sleepTime = de.RetryAfter;
                    }
                    else
                    {
                        throw;
                    }
                }

                await Task.Delay(sleepTime);
            }

            return default(T);
        }

        internal static bool TryGetDocumentClientException(Exception originalEx, out DocumentClientException documentClientEx)
        {
            documentClientEx = originalEx as DocumentClientException;

            if (documentClientEx != null)
            {
                return true;
            }

            AggregateException ae = originalEx as AggregateException;
            if (ae == null)
            {
                return false;
            }

            documentClientEx = ae.InnerException as DocumentClientException;

            return documentClientEx != null;
        }
    }
}
