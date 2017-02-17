// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text;
using Microsoft.Azure.Devices;

namespace Microsoft.Azure.WebJobs.Extensions.IoTHub.Converters
{
    internal class StringToMessage : IConverter<string, Message>
    {
        public Message Convert(string input)
        {
            return new Message(Encoding.UTF8.GetBytes(input));
        }
    }
}
