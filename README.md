Azure WebJobs SDK Extensions
===
This repo contains binding extensions for the **Azure WebJobs SDK**. See the [Azure WebJobs SDK repo](https://github.com/Azure/azure-webjobs-sdk) for more information. The binding extensions in this repo are available as the **Microsoft.Azure.WebJobs.Extensions** [nuget package](http://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions). Note: some of the extensions in this repo (like SendGrid, WebHooks, etc.) live in their own separate nuget packages following a standard naming scheme (e.g. Microsoft.Azure.WebJobs.Extensions.SendGrid).

The [wiki](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki) contains information on how to **author your own binding extensions**. See the [Binding Extensions Overview](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Binding-Extensions-Overview) for more details. A [sample project](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Program.cs) is also provided that demonstrates the bindings in action.

The extensions included in this repo include the following:

###TimerTrigger###

A fully featured Timer trigger that supports cron expressions, as well as other schedule expressions. A couple of examples:

```csharp
public static void CronJob([TimerTrigger("0 */1 * * * *")] TimerInfo timer)
{
    Console.WriteLine("Cron job fired!");
}

public static void TimerJob([TimerTrigger("00:00:30")] TimerInfo timer)
{
    Console.WriteLine("Timer job fired!");
}
```
The TimerTrigger also handles multi-instance scale out automatically - only a single instance of a particular timer function will be running across all instances (you don't want multiple instances to process the same timer event).

For more information, see the [Timer samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/TimerSamples.cs).

###FileTrigger / File###

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

For more information, see the [File samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/FileSamples.cs).

###SendGrid###

A [SendGrid](https://sendgrid.com) binding that allows you to easily send emails after your job functions complete. Simply add your SendGrid ApiKey as an app setting, and you can write functions like the below which demonstrates full route binding for message fields. In this scenario, an email is sent each time a new order is successfully placed. The message fields are automatically bound to the `CustomerEmail/CustomerName/OrderId` properties of the Order object that triggered the function.

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

The above messages are fully declarative, but you can also set the message properties in your job function code (e.g. add message attachments, etc.). For more information on the SendGrid binding, see the [SendGrid samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/SendGridSamples.cs).

###WebHooks###

A WebHook trigger that allows you to write job functions that can be invoked by http requests. Here's an example job function that will be invoked whenever an issue in a source GitHub repo is created or modified:

```csharp
public static void IssueChanged([WebHookTrigger] string body)
{
    // Parse and process the JSON body sent to us by GitHub
}
```

The web hook URL used to invoke the function is configured in the source repo ([more on GitHub web hooks here](https://developer.github.com/webhooks/)). Details on how to construct this URL can be found below.

GitHub is just one example. Any event source supporting WebHooks can be used. Another popular source is [IFTTT](https://ifttt.com/). Using IFTTT ("If This, Then That"), you can configure your webjob to be invoked when stock prices change, on events coming from Facebook, Instagram, YouTube, EBay etc., or even when someone alters your Nest thermostat setting! WebHooks opens the WebJobs SDK up to a huge new set of triggers, complimenting the existing first class SDK triggers (and extension triggers). Here's an IFTTT triggered function that will get invoked whenever a new article is added to [Pocket](https://getpocket.com/) in the browser for later reading. The function demonstrates model binding to a custom type, and pushes the articles to blob storage:

```csharp
public static void NewArticle(
    [WebHookTrigger] Article article,
    [Blob("articles/{Title}", FileAccess.Write)] TextWriter output,
    TraceWriter trace)
{
    output.WriteLine(article.Url);
    output.WriteLine(article.Excerpt);

    trace.Info(string.Format("New article added. '{0}'", article.Title));
}
```

When running in Azure Web Apps, your WebHook job will be running in the context of a [Continuous WebJob](https://github.com/projectkudu/kudu/wiki/Web-jobs). This host will accept (and **authenticate**) incoming requests and forward them to the SDK JobHost. The URL used to trigger a WebHook function has the following format:

    https://{uid}:{pwd}@{site}/api/continuouswebjobs/{job}/passthrough/{*path}

To manually construct your WebHook URL, you need to replace the following tokens:
* **uid** : This is the user ID from your SCM/Kudu credentials
* **pwd** : This is the password from your SCM/Kudu credentials
* **site** : Your SCM site (e.g. myapp.scm.azurewebsites.net)
* **job** : The name of your Continuous WebJob
* **path** : This is the route identifying the specific WebHook function to invoke. By convention, this is {ClassName}/{MethodName}, but can be overridden/specified explicitly via the WebHookTrigger attribute.

In addition to functions using the WebHook trigger, you can invoke **any** of your job functions via an http request. When resolving an incoming POST request (only POST is supported), if the route doesn't match a WebHookTrigger decorated function, a search is performed for a function matching the {ClassName}/{MethodName} convention described above. If found that function is invoked, and the function parameters are parsed from the JSON body of the request. An example of this can bee seen in the tests [here](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/test/WebJobs.Extensions.Tests/WebHooks/WebHookEndToEndTests.cs#L72). This ability to invoke your functions via REST requests opens the door for automation scenarios, and compliments the way you can invoke/replay functions via the WebJobs Dashboard.

For more information, see the [WebHook samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/WebHookSamples.cs).
