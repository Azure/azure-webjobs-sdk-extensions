Azure WebJobs SDK Extensions
===
This repo contains binding extensions to the **Azure WebJobs SDK**. See the [Azure WebJobs SDK repo](https://github.com/Azure/azure-webjobs-sdk) for more information.

The binding extensions in this repo are available as the **Microsoft.Azure.WebJobs.Extensions** nuget package on the [Azure AppService MyGet feed](https://www.myget.org/gallery/azure-appservice).

The wiki also contains information on how to author your own binding extensions. See the [Binding Extensions Overview](https://github.com/Azure/azure-webjobs-sdk-extensions/wiki/Binding-Extensions-Overview) for more details.

The extensions included in this repo include:

###TimerTrigger###

A fully featured Timer trigger that supports cron expressions, as well as other schedule expressions. A couple of examples:

    public static void CronJob([TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo)
    {
        Console.WriteLine("Cron job fired!");
    }

    public static void TimerJob([TimerTrigger("00:00:30")] TimerInfo timerInfo)
    {
        Console.WriteLine("Timer job fired!");
    }
    
###FileTrigger###

A trigger that monitors for file additions/changes to a particular directory, and triggers a job function when they occur. Here's an example that monitors for any *.dat files added to a particular directory, uploads them to blob storage, and deletes the files automatically after successful processing. The FileTrigger also handles multi-instance scale out automatically - only a single instance will process a particular file event.

    public static void ImportFile(
        [FileTrigger(@"import\{name}", "*.dat", autoDelete: true)] FileStream file,
        [Blob(@"processed/{name}")] CloudBlockBlob output,
        string name)
    {
        output.UploadFromStream(file);
        file.Close();

        log.WriteLine(string.Format("Processed input file '{0}'!", name));
    }
