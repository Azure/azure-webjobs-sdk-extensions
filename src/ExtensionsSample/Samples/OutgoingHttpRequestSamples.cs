// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;

namespace ExtensionsSample.Samples
{
    public static class OutgoingHttpRequestSamples
    {
        public static void Foo(
            [TimerTrigger("00:01")] TimerInfo timer,
            [OutgoingHttpRequest(@"http://requestb.in/1mxonf61")] TextWriter writer)
        {
            writer.Write("Hello from writer!");
        }
    }
}
