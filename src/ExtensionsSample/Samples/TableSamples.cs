// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Sample.Extension;

namespace ExtensionsSample
{
    public static class TableSamples
    {
        // Demonstrates use of a custom table binding extension to bind Table
        // to a custom type (Table<T>).
        public static void CustomBinding([Table("sampletable")] Table<Person> table)
        {
            Person entity = new Person()
            {
                Name = "Sample"
            };
            table.Add(entity);
            table.Delete(entity);
        }
    }
}
