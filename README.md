﻿Azure WebJobs SDK Extensions
===
|Branch|Status|
|---|---|
|master|[![Build status](https://ci.appveyor.com/api/projects/status/5mqrok4j3l89cnvx/branch/master?svg=true)](https://ci.appveyor.com/project/appsvc/azure-webjobs-sdk-extensions/branch/master)|
|dev|[![Build status](https://ci.appveyor.com/api/projects/status/5mqrok4j3l89cnvx/branch/dev?svg=true)](https://ci.appveyor.com/project/appsvc/azure-webjobs-sdk-extensions/branch/dev)|


This repo contains binding extensions for the **Azure WebJobs SDK**. See the [Azure WebJobs SDK repo](https://github.com/Azure/azure-webjobs-sdk) for more information. The binding extensions in this repo are available as the **Microsoft.Azure.WebJobs.Extensions** [nuget package](http://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions). **Note**: some of the extensions in this repo (like SendGrid, etc.) live in their own separate nuget packages following a standard naming scheme (e.g. Microsoft.Azure.WebJobs.Extensions.SendGrid). Also note that some of the features discussed here or in the wiki may still be in **pre-release**. To access those features you may need to pull the very latest pre-release packages from our "nightlies" package feed ([instructions here](https://github.com/Azure/azure-webjobs-sdk/wiki/%22Nightly%22-Builds)).

The [wiki](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki) contains information on how to **author your own binding extensions**. See the [Binding Extensions Overview](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Binding-Extensions-Overview) for more details. A [sample project](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Program.cs) is also provided that demonstrates the bindings in action.

Extensions all follow the same "add" pattern for registration - after referencing the package the extension lives in, you call the corresponding "add" method to register the extension. These "add" methods are extension methods that often take optional configuration objects to customize the behavior of the extension. For example, the `b.AddAzureStorage()` call below registers the Azure Storage extension.

```csharp
var builder = new HostBuilder();
builder.ConfigureWebJobs(b =>
{
    b.AddAzureStorageCoreServices();
    b.AddAzureStorage();
});
builder.ConfigureLogging((context, b) =>
{
    b.AddConsole();
});

var host = builder.Build();
using (host)
{
    await host.RunAsync();
}
```

## Other Extension Repositories
Not all extensions for webjobs live here. Over time we expect them to move towards having their own ship cycle and repository. You can find other Azure owned extensions using [this github query](https://github.com/Azure?utf8=✓&q=functions%20extension). Right now there are:
- https://github.com/Azure/azure-functions-durable-extension
- https://github.com/Azure/azure-functions-eventgrid-extension
- https://github.com/Azure/azure-functions-iothub-extension

## Extensions

The extensions included in this repo include the following. This is not an exhaustive list - see the **ExtensionsSample** project in this repo for more information extension samples.

### TimerTrigger

A fully featured Timer trigger for scheduled jobs that supports cron expressions, as well as other schedule expressions. A couple of examples:

```csharp
// Runs once every 5 minutes
public static void CronJob([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
{
    Console.WriteLine("Cron job fired!");
}

// Runs immediately on startup, then every two hours thereafter
public static void StartupJob(
    [TimerTrigger("0 0 */2 * * *", RunOnStartup = true)] TimerInfo timerInfo)
{
    Console.WriteLine("Timer job fired!");
}

// Runs once every 30 seconds
public static void TimerJob([TimerTrigger("00:00:30")] TimerInfo timer)
{
    Console.WriteLine("Timer job fired!");
}

// Runs on a custom schedule. You implement Type MySchedule which is called on to
// return the next occurrence time as needed
public static void CustomJob([TimerTrigger(typeof(MySchedule))] TimerInfo timer)
{
    Console.WriteLine("Custom job fired!");
}
```
The TimerTrigger also handles multi-instance scale out automatically - only a single instance of a particular timer function will be running across all instances (you don't want multiple instances to process the same timer event).

The first example above uses a [cron expression](http://en.wikipedia.org/wiki/Cron#CRON_expression) to declare the schedule. Using these **6 fields** `{second} {minute} {hour} {day} {month} {day of the week}` you can express arbitrarily complex schedules very concisely. **Note**: the 6 field format including seconds is less common, so in the various cron expression docs you find online you'll have to adjust for the extra field.

To register the Timer extensions, call `config.UseTimers()` in your startup code. For more information, see the [TimerTrigger wiki page](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/TimerTrigger), and also the [Timer samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/TimerSamples.cs).

### FileTrigger / File

A trigger that monitors for file additions/changes to a particular directory, and triggers a job function when they occur. Here's an example that monitors for any *.dat files added to a particular directory, uploads them to blob storage, and deletes the files automatically after successful processing. The FileTrigger also handles multi-instance scale out automatically - only a single instance will process a particular file event. Also included is a non-trigger File binding allowing you to bind to input/output files.

```csharp
public static void ImportFile(
    [FileTrigger(@"import\{name}", "*.dat", autoDelete: true)] Stream file,
    [Blob(@"processed/{name}")] CloudBlockBlob output,
    string name)
{
    output.UploadFromStream(file);
    file.Close();

    log.WriteLine(string.Format("Processed input file '{0}'!", name));
}
```

To register the File extensions, call `config.UseFiles()` in your startup code. For more information, see the [File samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/FileSamples.cs).

### SendGrid

A [SendGrid](https://sendgrid.com) binding that allows you to easily send emails after your job functions complete. This extension lives in the **Microsoft.Azure.WebJobs.Extensions.SendGrid** package. Simply add your SendGrid ApiKey as an app setting or environment variable (use setting name `AzureWebJobsSendGridApiKey`), and you can write functions like the below which demonstrates full route binding for message fields. In this scenario, an email is sent each time a new order is successfully placed. The message fields are automatically bound to the `CustomerEmail/CustomerName/OrderId` properties of the Order object that triggered the function.

```csharp
public static void ProcessOrder(
    [QueueTrigger(@"samples-orders")] Order order,
    [SendGrid(
        To = "{CustomerEmail}",
        Subject = "Thanks for your order (#{OrderId})!",
        Text = "{CustomerName}, we've received your order ({OrderId})!")]
    out Mail message)
{
    // You can set additional message properties here
}
```

Here's another example showing how you can easily send yourself notification mails to your own admin address when your jobs complete. In this case, the default To/From addresses come from the global SendGridConfiguration object specified on startup, so don't need to be specified.

```csharp
public static void Purge(
    [QueueTrigger(@"purge-tasks")] PurgeTask task,
    [SendGrid(Subject = "Purge {Description} succeeded. Purged {Count} items")]
    out Mail message)
{
    // Purge logic here
}
```

The above messages are fully declarative, but you can also set the message properties in your job function code (e.g. add message attachments, etc.). 

To register the SendGrid extensions, call `config.UseSendGrid()` in your startup code. For more information on the SendGrid binding, see the [SendGrid samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/SendGridSamples.cs).

### ErrorTrigger

An **error trigger** that allows you to annotate functions to be automatically called by the runtime when errors occur. This allows you to set up email/text/etc. notifications to alert you when things are going wrong with your jobs.  Here's an example function that will be called whenever 10 errors occur within a 30 minute sliding window (throttled at a maximum of 1 notification per hour):

```csharp
public static void ErrorMonitor(
    [ErrorTrigger("0:30:00", 10, Throttle = "1:00:00")] TraceFilter filter, 
    TextWriter log)
{
    // send Text notification using IFTTT
    string body = string.Format("{{ \"value1\": \"{0}\" }}", filter.Message);
    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, WebNotificationUri)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };
    HttpClient.SendAsync(request);

    // log last 5 detailed errors to the Dashboard
    log.WriteLine(filter.GetDetailedMessage(5));
}
```

You can choose to send a alert text message to yourself, or a detailed email message, etc. The ErrorTrigger extension is part of the **Core extensions** can be registered on your JobHostConfiguration by calling `config.UseCore()`. In addition to setting up one or more **global error handlers** like the above, you can also specify **function specific error handlers** that will only handle erros for one function. This is done by naming convention based on an "ErrorHandler" suffix. For example, if your error function is named "**Import**ErrorHandler" and there is a function named "Import" in the same class, that error function will be scoped to errors for that function only:

```csharp
public static void Import(
    [FileTrigger(@"import\{name}")] Stream file,
    [Blob(@"processed/{name}")] CloudBlockBlob output)
{
    output.UploadFromStream(file);
}

public static void ImportErrorHandler(
    [ErrorTrigger("1:00:00", 5)] TraceFilter filter,
    TextWriter log)
{
    // Here you could send an error notification, etc.

    // log last 5 detailed errors to the Dashboard
    log.WriteLine(filter.GetDetailedMessage(3));
}
```

To register the Error extensions, call `config.UseCore()` in your startup code. For more information see the [Error Monitoring](http://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Error-Monitoring) wiki page, as well as the the [Error Monitoring Sample](http://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/ErrorMonitoringSamples.cs).

### Core Extensions

There are a set of triggers/bindings that can be registered by calling `config.UseCore()`. The Core extensions contain a set of general purpose bindings. For example, the **ErrorTrigger** binding discussed in its own section above is part of the Core extension. There is also a binding for `ExecutionContext` which allows you to access invocation specific system information in your function. Here's an example showing how to access the function **Invocation ID** for the function:

```csharp
public static void ProcessOrder(
    [QueueTrigger("orders")] Order order,
    TextWriter log,
    ExecutionContext context)
{
    log.WriteLine("InvocationId: {0}", context.InvocationId);
}
```

The invocation ID is used in the Dashboard logs, so having access to this programatically allows you to correlate an invocation to those logs. This might be useful if you're also logging to your own external system. To register the Core extensions, call `config.Core()` in your startup code.

### Azure Mobile Apps

A binding that allows you to easily create, read, and update records from an [Azure Mobile App](https://azure.microsoft.com/en-us/services/app-service/mobile/). This extension lives in **Microsoft.Azure.WebJobs.Extensions.MobileApps** nuget package. To configure the binding, add the Mobile App's URI (like `https://{yourapp}.azurewebsites.net`) as an app setting or environment variable using the setting name `AzureWebJobsMobileAppUri`. 

By default, the binding will only be able to interact with Mobile App tables that are configured to be Anonymous. However, you can configure an Api Key on the Mobile App by following the Api Key sample for the [Node SDK](https://github.com/Azure/azure-mobile-apps-node/tree/master/samples/api-key) and the guide for the [.NET SDK](https://github.com/Azure/azure-mobile-apps-net-server/wiki/Implementing-Application-Key). Once your Api Key is properly configured for your Mobile App, you can configure your binding to use it by setting an app setting or environment variable with the name `AzureWebJobsMobileAppApiKey`.

In this scenario, a record is created in the `Item` table each time a message appears in the queue. This specific sample uses an anonymous type for the record, but it could also have used `JObject` or any other type with a `public string Id` property.

```csharp
public static void MobileTableOutputSample(
    [QueueTrigger("sample")] QueueData trigger,
    [MobileTable(TableName = "Item")] out object item)
{    
    item = new { Text = "some sample text" };
}
```

The following sample performs a lookup based on the data in the queue trigger. The `RecordId` property value of the `QueueData` object is used to query the `Item` table of the Mobile App. If the record exists, it is provided in the `item` parameter. If not, the `item` parameter will be `null`. Inside the method, the `item` object is changed. This change is automatically sent back to the Mobile App when the method exits. This scenario uses the `Item` type, but it could also have used `JObject` or any type with a `public string Id` property.

```csharp
public static void MobileTableInputSample(
    [QueueTrigger("sample")] QueueData trigger,
    [MobileTable(TableName = "Item", Id = "{RecordId}")] Item item)
{    
    // item will be null if the record is not found
    if (item != null)
    {
        // Perform some processing...    
        item.IsProcessed = true;
    }
}
```

If you need more control over the interaction with your Mobile App, you can also use parameters of type `IMobileServiceTable`, `IMobileServiceTable<T>`, `IMobileServiceTableQuery<T>`, or even `IMobileServiceClient`. For example, the method below can be used to execute a more complex query against the `Item` table. Note that if you are using a type other than `object` or `JObject`, the `TableName` is not required as the underlying Mobile App Client SDK will determine the table name based on the type. However, if you need to use a table name that does not match your type, you can specify that name as the `TableName` and it will override the type name.

```csharp
public static void MobileTableSample(
    [QueueTrigger("sample")] QueueData trigger,
    [MobileTable] IMobileServiceTable<Item> itemTable)
{    
    IEnumerable<Item> processedItems = await itemTable.CreateQuery()
        .Where(i => i.IsProcessed && i.ProcessedAt < DateTime.Now.AddMinutes(-5))
        .ToListAsync();

    foreach (Item i in processedItems)
    {
        await table.DeleteAsync(i);
    }
}
```
### DocumentDB

Use an [Azure DocumentDB](https://azure.microsoft.com/en-us/services/documentdb/) binding to easily create, read, and update JSON documents from a WebJob. This extension lives in **Microsoft.Azure.WebJobs.Extensions.DocumentDB** nuget package. To configure the binding, add the DocumentDB connection string as an app setting or environment variable using the setting name `AzureWebJobsDocumentDBConnectionString`.

By default, the collection and database must exist before the binding runs or it will throw an Exception. You can configure the binding to automatically create your datatabase and collection by setting `CreateIfNotExists` to true. This property only applies to `out` parameters. For input (lookup) scenarios, if the database or collection do not exist, the parameter is returned as `null`. To define a partition key for automatically-created collections, set the `PartitionKey` property. To control the throughput of the collection, set the `CollectionThroughput` property. For more information on partition keys and throughput, see [Partitioning and scaling in Azure DocumentDB](https://azure.microsoft.com/en-us/documentation/articles/documentdb-partition-data/).

In this example, the `newItem` object is upserted into the `ItemCollection` collection of the `ItemDb` DocumentDB database. The collection will be automatically created if it does not exist, with a partition key of `/mypartition` and a throughput of `12000`.

```csharp
public static void InsertDocument(
    [QueueTrigger("sample")] QueueData trigger,
    [DocumentDB("ItemDb", "ItemCollection", CreateIfNotExists = true, PartitionKey = "/mypartition", CollectionThroughput = 12000)] out ItemDoc newItem)
{
    newItem = new ItemDoc()
    {
        Text = "sample text"
    };
}
```

The following sample performs a lookup based on the data in the queue trigger. The `DocumentId` and `PartitionKey` properties value of the `QueueData` object are used to query the `ItemCollection` document collection. The `PartitionKey` property is optional and does not need to be specified unless your collection has a partition key. If the document exists, it is provided in the `item` parameter. If not, the `item` parameter will be `null`. Inside the method, the `item` object is changed. This change is automatically sent back to the document collection when the method exits.

```csharp
public static void ReadDocument(
    [QueueTrigger("sample")] QueueData trigger,
    [DocumentDB("ItemDb", "ItemCollection", Id = "{DocumentId}", PartitionKey = "{PartitionKey}")] JObject item)
{
    item["text"] = "Text changed!";
}
```

#### Sql Query Support

If you need to make a query to return many Documents from Document DB, use the `SqlQuery` property on the `DocumentDBAttribute`. This property supports runtime binding, so the example below will replace `{QueueTrigger}` with the value from the queue message. In order to prevent injection attacks, any binding string used in the `SqlQuery` property is replaced with a [`SqlParameter`](https://azure.microsoft.com/en-us/blog/announcing-sql-parameterization-in-documentdb/) before being sent to your Document DB database. Queries must be of type `JArray` or `IEnumerable<T>`, where `T` is a type supported by Document DB (such as `Document`, `JObject`, or your own custom type). If you want to return all documents in a collection, you can remove the `SqlQuery` property and use `JArray` or `IEnumerable<T>` as your parameter type.
```csharp
public static void ReadDocument(
    [QueueTrigger("sample")] string trigger,
    [DocumentDB("ItemDb", "ItemCollection", SqlQuery = "SELECT c.id, c.fullName, c.department FROM c where c.department = {QueueTrigger}")] IEnumerable<JObject> documents)
{
    foreach(JObject doc in documents)
    {
        // do something
    }
}
```


If you need more control, you can also specify a parameter of type `DocumentClient`. The following example uses DocumentClient to query for all documents in `ItemCollection` and log their ids.

```csharp
public static void DocumentClient(
    [QueueTrigger("sample")] QueueData trigger,
    [DocumentDB] DocumentClient client,
    TraceWriter log)
{
    var collectionUri = UriFactory.CreateDocumentCollectionUri("ItemDb", "ItemCollection");
    var documents = client.CreateDocumentQuery(collectionUri);

    foreach (Document d in documents)
    {
        log.Info(d.Id);
    }
}
```

### Azure Notification Hubs

An [Azure Notification Hub](https://azure.microsoft.com/en-us/services/notification-hubs/) binding allows you to easily send push notifications to any platform. This extension lives in **Microsoft.Azure.WebJobs.Extensions.NotificationHubs** nuget package. To configure the binding, add the NotificationHubs namespace connection string as an app setting or environment variable using the setting name `AzureWebJobsNotificationHubsConnectionString` and add the name of the NotificationHub as an app setting or environment variable using the setting name `AzureWebJobsNotificationHubName`.

Azure Notification Hub must be configured for the Platform Notifications Services (PNS) you want to use. For more information on configuring an Azure Notification Hub and developing a client applications that register for notifications, see [Getting started with Notification Hubs] (https://azure.microsoft.com/en-us/documentation/articles/notification-hubs-windows-store-dotnet-get-started/) and click your target client platform at the top.

The following sample sends windows toast notification when a new file is uploaded to a blob

```csharp
 public static void SendNotification(
    [BlobTrigger("sample/{name}.{ext}")] Stream input, string name, string ext
    [NotificationHub] out Notification notification)
{
    string message = string.Format("File {0}.{1} uploaded to Blob container sample", name, ext);
    string toastPayload = string.Format("<toast><visual><binding template=\"ToastText01\"><text id=\"1\">{0}</text></binding></visual></toast>", message);
    notification = new WindowsNotification(toastPayload);
}
```

Here's an example for sending [template notification] (https://msdn.microsoft.com/en-us/library/azure/dn530748.aspx) to an userId [tag] (https://azure.microsoft.com/en-us/documentation/articles/notification-hubs-routing-tag-expressions/) in the queue trigger. The `userId` is a property value of the `QueueData` object.

```csharp
public static void SendTemplateNotification(
    [QueueTrigger("queue")] QueueData queueData,
    [NotificationHub(TagExpression = "{userId}")] out string messageProperties)
{
    messageProperties = "{\"message\":\"Hello\",\"location\":\"Redmond\"}";
}
```

### Twilio SMS

A [Twilio](https://twilio.com) binding that allows you to easily send SMS messages from your job functions. This extension lives in the **Microsoft.Azure.WebJobs.Extensions.Twilio** package. Simply add your Twilio Account SID and Auth Token as app settings or environment variables (with settings named `AzureWebJobsTwilioAccountSid` and `AzureWebJobsTwilioAuthToken`, respectively), and you can write functions like the below which demonstrates full route binding for message fields. In this scenario, an SMS message is sent each time a new order is successfully placed. The message fields are automatically bound to the `CustomerPhoneNumber/StorePhoneNumber/CustomerName/OrderId` properties of the Order object that triggered the function.

```csharp
 public static void ProcessOrder(
    [QueueTrigger(@"samples-orders")] Order order,
    [TwilioSms(
        To = "{CustomerPhoneNumber}",
        From = "{StorePhoneNumber}",
        Body = "{CustomerName}, we've received your order ({OrderId}) and have begun processing it!")]
    out SMSMessage message)
{
    // You can set additional message properties here
    message = new SMSMessage();
}
```

Here's another example showing how you can easily send yourself notification mails to your own admin address when your jobs complete. In this case, the default To/From addresses come from the global TwilioSmsConfiguration object specified on startup, so don't need to be specified.

```csharp
public static void Purge(
    [QueueTrigger(@"purge-tasks")] PurgeTask task,
    [TwilioSms(Body = "Purge {Description} succeeded. Purged {Count} items")]
    out SMSMessage message)
{
    // Purge logic here
}
```

The above messages are fully declarative, but you can also set the message properties in your job function code (e.g. From number, To number, Body, etc.). 

To register the Twilio SMS extensions, call `config.UseTwilioSms()` in your startup code. For more information on the Twilio binding, see the [Twilio samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/TwilioSamples.cs).

## License

This project is under the benevolent umbrella of the [.NET Foundation](http://www.dotnetfoundation.org/) and is licensed under [the MIT License](LICENSE.txt)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
