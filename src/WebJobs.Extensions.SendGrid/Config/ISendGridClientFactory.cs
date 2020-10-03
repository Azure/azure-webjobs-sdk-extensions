// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using SendGrid;

namespace Microsoft.Azure.WebJobs.Extensions.SendGrid.Config
{
    internal interface ISendGridClientFactory
    {
        ISendGridClient Create(string apiKey);
    }
}
