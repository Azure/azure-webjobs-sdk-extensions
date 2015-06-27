using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Files;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;

namespace Microsoft.Azure.WebJobs.Files.Listeners
{
    internal class FileListenerFactory : IListenerFactory
    {
        private readonly FileTriggerAttribute _attribute;
        private readonly ITriggeredFunctionExecutor _executor;
        private readonly FilesConfiguration _config;

        public FileListenerFactory(FilesConfiguration config, FileTriggerAttribute attribute, ITriggeredFunctionExecutor executor)
        {
            _config = config;
            _attribute = attribute;
            _executor = executor;
        }

        public Task<IListener> CreateAsync(ListenerFactoryContext context)
        {
            FileListener listener = new FileListener(_config, _attribute, _executor);
            return Task.FromResult<IListener>(listener);
        }
    }
}
