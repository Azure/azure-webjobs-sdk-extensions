// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace ExtensionsSample.Models
{
    public class Item
    {
        public string Id { get; set; }

        public string Text { get; set; }

        public bool IsProcessed { get; set; }

        public DateTimeOffset ProcessedAt { get; set; }

        // Mobile table properties
        public DateTimeOffset CreatedAt { get; set; }
    }
}
