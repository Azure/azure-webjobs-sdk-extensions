using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Framework;
using Microsoft.Azure.WebJobs.Host.Triggers;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal class FileTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly FilesConfiguration _config;

        public FileTriggerAttributeBindingProvider(FilesConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            _config = config;
        }

        /// <inheritdoc/>
        public Task<ITriggerBinding> TryCreateAsync(TriggerBindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            FileTriggerAttribute attribute = parameter.GetCustomAttribute<FileTriggerAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            // next, verify that the type is one of the types we support
            IEnumerable<Type> types = StreamValueBinder.SupportedTypes.Union(new Type[] { typeof(FileStream), typeof(FileSystemEventArgs), typeof(FileInfo) });
            if (!ValueBinder.MatchParameterType(context.Parameter, types))
            {
                return Task.FromResult<ITriggerBinding>(null);
            }

            ITriggerBinding<FileSystemEventArgs> binding = new FileTriggerBinding(_config, parameter);
            if (binding == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Can't bind FileTriggerAttribute to type '{0}'.", parameter.ParameterType));
            }

            return Task.FromResult<ITriggerBinding>(binding);
        }
    }
}
