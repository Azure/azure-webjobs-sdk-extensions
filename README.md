Azure WebJobs SDK Extensions
===

This repo contains binding extensions for the **Azure WebJobs SDK**. See the [Azure WebJobs SDK repo](https://github.com/Azure/azure-webjobs-sdk) for more information. The binding extensions in this repo are available as the **Microsoft.Azure.WebJobs.Extensions** [nuget package](http://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions). **Note**: some of the extensions in this repo (like SendGrid, WebHooks, etc.) live in their own separate nuget packages following a standard naming scheme (e.g. Microsoft.Azure.WebJobs.Extensions.SendGrid). Also note that some of the features discussed here or in the wiki may still be in **pre-release**. To access those features you may need to pull the very latest pre-release packages from our "nightlies" package feed ([instructions here](https://github.com/Azure/azure-webjobs-sdk/wiki/%22Nightly%22-Builds)).

The [wiki](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki) contains information on how to **author your own binding extensions**. See the [Binding Extensions Overview](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Binding-Extensions-Overview) for more details. A [sample project](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Program.cs) is also provided that demonstrates the bindings in action.

Extensions all follow the same "using" pattern for registration - after referencing the package the extension lives in, you call the corresponding "using" method to register the extension. These "using" methods are extension methods on `JobHostConfiguration` and often take optional configuration objects to customize the behavior of the extension. For example, the `config.UseSendGrid(...)` call below registers the SendGrid extension using the specified configuration options.

```csharp
JobHostConfiguration config = new JobHostConfiguration();
config.Tracing.ConsoleLevel = TraceLevel.Verbose;

config.UseFiles();
config.UseTimers();
config.UseSendGrid(new SendGridConfiguration()
{
    FromAddress = new MailAddress("orders@webjobssamples.com", "Order Processor")
});
config.UseWebHooks();

JobHost host = new JobHost(config);
host.RunAndBlock();
```

##Extensions

The extensions included in this repo include the following. This is not an exhaustive list - see the **ExtensionsSample** project in this repo for more information extension samples.

###TimerTrigger

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

###FileTrigger / File

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

###SendGrid

A [SendGrid](https://sendgrid.com) binding that allows you to easily send emails after your job functions complete. This extension lives in the **Microsoft.Azure.WebJobs.Extensions.SendGrid** package. Simply add your SendGrid ApiKey as an app setting or environment variable (use setting name `AzureWebJobsSendGridApiKey`), and you can write functions like the below which demonstrates full route binding for message fields. In this scenario, an email is sent each time a new order is successfully placed. The message fields are automatically bound to the `CustomerEmail/CustomerName/OrderId` properties of the Order object that triggered the function.

```csharp
public static void ProcessOrder(
    [QueueTrigger(@"samples-orders")] Order order,
    [SendGrid(
        To = "{CustomerEmail}",
        Subject = "Thanks for your order (#{OrderId})!",
        Text = "{CustomerName}, we've received your order ({OrderId})!")]
    SendGridMessage message)
{
    // You can set additional message properties here
}
```

Here's another example showing how you can easily send yourself notification mails to your own admin address when your jobs complete. In this case, the default To/From addresses come from the global SendGridConfiguration object specified on startup, so don't need to be specified.

```csharp
public static void Purge(
    [QueueTrigger(@"purge-tasks")] PurgeTask task,
    [SendGrid(Subject = "Purge {Description} succeeded. Purged {Count} items")]
    SendGridMessage message)
{
    // Purge logic here
}
```

The above messages are fully declarative, but you can also set the message properties in your job function code (e.g. add message attachments, etc.). 

To register the SendGrid extensions, call `config.UseSendGrid()` in your startup code. For more information on the SendGrid binding, see the [SendGrid samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/SendGridSamples.cs).

###ErrorTrigger

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

###Core Extensions

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

## License

This project is under the benevolent umbrella of the [.NET Foundation](http://www.dotnetfoundation.org/) and is licensed under [the MIT License](LICENSE.txt)
