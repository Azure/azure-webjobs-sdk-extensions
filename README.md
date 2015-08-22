Azure WebJobs SDK Extensions
===
This repo contains binding extensions to the **Azure WebJobs SDK**. See the [Azure WebJobs SDK repo](https://github.com/Azure/azure-webjobs-sdk) for more information. The binding extensions in this repo are available as the **Microsoft.Azure.WebJobs.Extensions** [nuget package](http://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions). Note: some of the extensions in this repo (like SendGrid) live in their own separate nuget packages following a standard naming scheme (e.g. Microsoft.Azure.WebJobs.Extensions.SendGrid).

The [wiki](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki) contains information on how to **author your own binding extensions**. See the [Binding Extensions Overview](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Binding-Extensions-Overview) for more details. A [sample project](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/Program.cs) is also provided that demonstrates the bindings in action.

The extensions included in this repo include the following:

###TimerTrigger###

A fully featured Timer trigger that supports cron expressions, as well as other schedule expressions. A couple of examples:

    public static void CronJob([TimerTrigger("0 */1 * * * *")] TimerInfo timer)
    {
        Console.WriteLine("Cron job fired!");
    }

    public static void TimerJob([TimerTrigger("00:00:30")] TimerInfo timer)
    {
        Console.WriteLine("Timer job fired!");
    }
    
For more information, see the [Timer samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/TimerSamples.cs).
    
###FileTrigger / File###

A trigger that monitors for file additions/changes to a particular directory, and triggers a job function when they occur. Here's an example that monitors for any *.dat files added to a particular directory, uploads them to blob storage, and deletes the files automatically after successful processing. The FileTrigger also handles multi-instance scale out automatically - only a single instance will process a particular file event. Also included is a non-trigger File binding allowing you to bind to input/output files.

    public static void ImportFile(
        [FileTrigger(@"import\{name}", "*.dat", autoDelete: true)] Stream file,
        [Blob(@"processed/{name}")] CloudBlockBlob output,
        string name)
    {
        output.UploadFromStream(file);
        file.Close();

        log.WriteLine(string.Format("Processed input file '{0}'!", name));
    }

For more information, see the [File samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/FileSamples.cs).

###SendGrid###

A [SendGrid](https://sendgrid.com) binding that allows you to easily send emails after your job functions complete. Simply add your SendGrid ApiKey as an app setting, and you can write functions like the below which demonstrates full route binding for message fields. In this scenario, an email is sent each time a new order is successfully placed. The message fields are automatically bound to the CustomerEmail/CustomerName/OrderId properties of the Order object that triggered the function.

    public static void ProcessOrder(
        [QueueTrigger(@"samples-orders")] Order order,
        [SendGrid(
            To = "{CustomerEmail}",
            Subject = "Thanks for your order (#{OrderId})!",
            Text = "{CustomerName}, we've received your order ({OrderId}) and have begun processing it!")]
        SendGridMessage message)
    {
        // You can set additional message properties here
    }

Here's another example showing how you can easily send yourself notification mails to your own admin address when your jobs complete. In this case, the default To/From addresses come from the global SendGridConfiguration object specified on startup, so don't need to be specified.

    public static void Purge(
        [QueueTrigger(@"purge-tasks")] PurgeTask task,
        [SendGrid(Subject = "Purge {Description} succeeded. Purged {Count} items")]
        SendGridMessage message)
    {
        // Purge logic here
    }

The above messages are fully declarative, but you can also set the message properties in your job function code (e.g. add message attachments, etc.). For more information on the SendGrid binding, see the [SendGrid samples](https://github.com/Azure/azure-webjobs-sdk-extensions/blob/master/src/ExtensionsSample/SendGridSamples.cs).    
