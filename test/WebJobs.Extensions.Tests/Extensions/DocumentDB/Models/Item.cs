// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models
{
    public class Item
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string Text { get; set; }
    }
}
