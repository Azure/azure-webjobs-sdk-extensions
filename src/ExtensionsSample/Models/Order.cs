// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace ExtensionsSample
{
    // { "OrderId": "12345", "CustomerName": "John Doe", "CustomerEmail": "john@johndoe.net" }
    public class Order
    {
        public string OrderId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerPhoneNumber { get; set; }
        public string StorePhoneNumber { get; set; }
    }
}
