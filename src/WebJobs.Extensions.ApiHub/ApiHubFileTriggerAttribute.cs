namespace Microsoft.Azure.WebJobs
{
    public class ApiHubFileTriggerAttribute : ApiHubFileAttribute
    {
        public ApiHubFileTriggerAttribute(string key, string path)
            : base(key, path)
        {
        }
    }
}
