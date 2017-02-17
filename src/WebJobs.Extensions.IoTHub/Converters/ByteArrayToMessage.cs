// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.Devices;

namespace Microsoft.Azure.WebJobs.Extensions.IoTHub.Converters
{
    internal class ByteArrayToMessage : IConverter<byte[], Message>
    {
        public Message Convert(byte[] input)
        {
            return new Message(input);
        }
    }
}
