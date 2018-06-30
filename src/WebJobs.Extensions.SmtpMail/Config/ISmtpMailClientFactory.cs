// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Client;

namespace Microsoft.Azure.WebJobs.Extensions.Config
{
    internal interface ISmtpMailClientFactory
    {
        ISmtpMailClient Create(string connectionString);
    }
}
