Azure WebJobs SDK Extensions
===
This repo contains binding extensions for the **Azure WebJobs SDK**. See the [Azure WebJobs SDK repo](https://github.com/Azure/azure-webjobs-sdk) for more information. The binding extensions in this repo are available as the **Microsoft.Azure.WebJobs.Extensions** [nuget package](http://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions). Note: some of the extensions in this repo (like SendGrid, WebHooks, etc.) live in their own separate nuget packages following a standard naming scheme (e.g. Microsoft.Azure.WebJobs.Extensions.SendGrid).

The [wiki](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki) contains information on how to **author your own binding extensions**. See the [Binding Extensions Overview](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Binding-Extensions-Overview) for more details. A [sample project](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Program.cs) is also provided that demonstrates the bindings in action.

Extensions all follow the same "using" pattern for registration - after referencing the package the extension lives in, you call the corresponding "using" method to register the extension. These "using" methods are extension methods on `JobHostConfiguration` and often take optional configuration objects to customize the behavior of the extension. For example, the `config.UseSendGrid(...)` call below registers the SendGrid extension using the specified configuration options.

```csharp
JobHostConfiguration config = new JobHostConfiguration();
config.Tracing.ConsoleLevel = TraceLevel.Verbose;

config.UseFiles
config.UseTimers();
config.UseSendGrid(new SendGridConfiguration()
{
    FromAddress = new MailAddress("orders@webjobssamples.com", "Order Processor")
});
config.UseWebHooks();

JobHost host = new JobHost(config);
host.RunAndBlock();
```

The extensions included in this repo include the following:

###TimerTrigger###

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
    Console.WriteLine("Customm job fired!");
}
```
The TimerTrigger also handles multi-instance scale out automatically - only a single instance of a particular timer function will be running across all instances (you don't want multiple instances to process the same timer event).

The first example above uses a [cron expression](http://code.google.com/p/ncrontab/wiki/CrontabExpression) to declare the schedule. Using these 6 fields `{second} {minute} {hour} {day} {month} {day of the week}` you can express arbitrarily complex schedules very concisely.

To register the Timer extensions, call `config.UseTimers()` in your startup code. For more information, see the [Timer samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/TimerSamples.cs).

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

To register the File extensions, call `config.UseFiles()` in your startup code. For more information, see the [File samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/FileSamples.cs).

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

The above messages are fully declarative, but you can also set the message properties in your job function code (e.g. add message attachments, etc.). 

To register the SendGrid extensions, call `config.UseSendGrid()` in your startup code. For more information on the SendGrid binding, see the [SendGrid samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/SendGridSamples.cs).

###ErrorTrigger###

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
    [ErrorTrigger] TraceEvent error, string message, TextWriter log)
{
    // Here you could send an error notification, etc.

    log.WriteLine(string.Format("{0} : {1}", message, error.ToString()));
}
```

To register the Error extensions, call `config.UseCore()` in your startup code. For more information see the [Error Monitoring](http://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Error-Monitoring) wiki page, as well as the the [Error Monitoring Sample](http://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/ErrorMonitoringSamples.cs).

###WebHooks###

A WebHook trigger that allows you to write job functions that can be invoked by HTTP requests. Here's an example job function that will be invoked whenever an issue in a source GitHub repo is created or modified:

```csharp
public static void GitHub([WebHookTrigger] string body, TraceWriter trace)
{
    dynamic issueEvent = JObject.Parse(body);

    trace.Info(string.Format("GitHub WebHook invoked - Issue: '{0}', Action: '{1}', ",
        issueEvent.issue.title, issueEvent.action));
}
```

The web hook URL used to invoke the function is configured in the source repo ([more on GitHub web hooks here](https://developer.github.com/webhooks/)). Details on how to construct this URL can be found below. You can also find a **complete step-by-step walkthrough** for setting up WebJob WebHooks [here](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/WebHooks-Walkthrough).

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
* **uid** : This is the user ID from your publishing credentials. You can download your publishing credentials from the portal [as detailed in this blog post](http://blog.davidebbo.com/2015/05/scheduled-webjob.html)
* **pwd** : This is the password from your publising credentials
* **site** : Your SCM site (e.g. myapp.scm.azurewebsites.net)
* **job** : The name of your Continuous WebJob
* **path** : This is the route identifying the specific WebHook function to invoke. By convention, this is {ClassName}/{MethodName}, but can be overridden/specified explicitly via the WebHookTrigger attribute.

In addition to functions using the WebHook trigger, you can invoke **any** of your job functions via an http request. When resolving an incoming POST request, if the route doesn't match a WebHookTrigger decorated function, a search is performed for a function matching the {ClassName}/{MethodName} convention described above. If found, that function is invoked, and the function parameters are parsed from the JSON body of the request. An example of this can be seen [in the walkthrough](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/WebHooks-Walkthrough). This ability to invoke your functions via HTTP requests opens the door for automation scenarios, and compliments the way you can invoke/replay functions via the WebJobs Dashboard.

Support for the [ASP.NET WebHooks SDK](http://blogs.msdn.com/b/webdev/archive/2015/09/04/introducing-microsoft-asp-net-webhooks-preview.aspx) is also built in. That SDK provides WebHook Receiver classes that handle the diverse WebHook authentication schemes of various providers. For providers that the SDK supports it is recommended that you use those receivers for authentication. For example, to leverage this for GitHub, you would:

* reference the **Microsoft.AspNet.WebHooks.Receivers.GitHub** package
* add the **MS_WebHookReceiverSecret_GitHub** app setting containing your secret
* set the same shared secret on your GitHub WebHook
* add [one line of code](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Program.cs#L46) to register the receiver on job startup
* use the overload of WebHookTriggerAttribute that takes a reveiver and optional id ([as in this example](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/WebHookSamples.cs#L67))
* use a corresponding route format {receiver}/{id} for the WebHook in GitHub (the "id" portion is optional). I.e. for this example the route would be "github/issues"

That's it - whenever a request comes in for your WebHook, the WebHook reveiver will perform all the authentication checks and your job function will *only* be invoked if the request is authenticated. For more information on the various ASP.NET WebHooks SDK reveivers supported, see their documentation.

To register the File extensions, call `config.UseWebHooks()` in your startup code. For more information, see the [WebHook samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Samples/WebHookSamples.cs).
