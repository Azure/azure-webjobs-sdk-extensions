// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal static class DocumentDBUtility
    {
        public static async Task ExecuteAndIgnoreStatusCodeAsync(HttpStatusCode statusToIgnore, Func<Task> function)
        {
            try
            {
                await function();
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode != statusToIgnore)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Execute the function with retries on throttle.
        /// </summary>
        /// <typeparam name="T">The type of return value from the execution.</typeparam>
        /// <param name="function">The function to execute.</param>
        /// <param name="ignoreNotFound">If true, NotFound status codes will be ignored.</param>
        /// <returns>The response from the execution.</returns>
        // Taken from: https://github.com/ryancrawcour/azure-documentdb-dotnet/blob/e7dd2f685554b0a9def63c8925f8e3ef2ad3bff8/samples/code-samples/Shared/Util/DocumentClientHelper.cs
        public static async Task<T> ExecuteWithRetriesAsync<T>(Func<Task<T>> function, bool ignoreNotFound = false)
        {
            TimeSpan sleepTime = TimeSpan.Zero;

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
                        if (de.StatusCode == HttpStatusCode.NotFound &&
                            ignoreNotFound)
                        {
                            break;
                        }

                        if ((int)de.StatusCode != 429)
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
            }

            return default(T);
        }

        private static bool TryGetDocumentClientException(Exception originalEx, out DocumentClientException documentClientEx)
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
