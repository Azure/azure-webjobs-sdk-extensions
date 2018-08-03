﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace CosmosDBSample
{
    public class ItemDoc
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("partition")]
        public string Partition { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("isCompleted")]
        public bool IsCompleted { get; set; }
    }
}
