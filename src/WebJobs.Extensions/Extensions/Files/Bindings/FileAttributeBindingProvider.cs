using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Framework;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal class FileAttributeBindingProvider : IBindingProvider
    {
        private readonly FilesConfiguration _config;

        public FileAttributeBindingProvider(FilesConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            _config = config;
        }

        /// <inheritdoc/>
        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // Determine whether we should bind to the current parameter
            ParameterInfo parameter = context.Parameter;
            FileAttribute attribute = parameter.GetCustomAttribute<FileAttribute>(inherit: false);
            if (attribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            if (!CanBind(context))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, 
                    "Can't bind FileAttribute to type '{0}'.", parameter.ParameterType));
            }

            return Task.FromResult<IBinding>(new FileBinding(_config, parameter));
        }

        private static bool CanBind(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // first, verify the file path binding (if it contains binding parameters)
            ParameterInfo parameter = context.Parameter;
            FileAttribute attribute = parameter.GetCustomAttribute<FileAttribute>(inherit: false);
            BindablePath path = new BindablePath(attribute.Path);
            path.ValidateContractCompatibility(context.BindingDataContract);

            // next, verify that the type is one of the types we support
            IEnumerable<Type> types = StreamValueBinder.SupportedTypes.Union(new Type[] { typeof(FileStream) });
            return ValueBinder.MatchParameterType(parameter, types);
        }
    }
}
