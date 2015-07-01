using Microsoft.Azure.WebJobs;
using Sample.Extension;

namespace ExtensionsSample
{
    public class Person
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public string Name { get; set; }
    }

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
