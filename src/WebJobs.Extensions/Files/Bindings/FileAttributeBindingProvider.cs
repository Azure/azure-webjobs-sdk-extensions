using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Common.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal class FileAttributeBindingProvider : IBindingProvider
    {
        // Define the supported argument bindings
        private static readonly CompositeArgumentBindingProvider<FileBindingInfo> InnerProvider =
            new CompositeArgumentBindingProvider<FileBindingInfo>(
                new FileOutputArgumentBindingProvider<string>(),
                new FileOutputArgumentBindingProvider<byte[]>(),
                new FileStreamArgumentBindingProvider());

        private readonly FilesConfiguration _config;

        public FileAttributeBindingProvider(FilesConfiguration config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            _config = config;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            FileAttribute fileAttribute = parameter.GetCustomAttribute<FileAttribute>(inherit: false);
            if (fileAttribute == null)
            {
                return Task.FromResult<IBinding>(null);
            }

            BindablePath path = new BindablePath(fileAttribute.Path);
            path.ValidateContractCompatibility(context.BindingDataContract);

            IArgumentBinding<FileBindingInfo> argumentBinding = InnerProvider.TryCreate(parameter);
            if (argumentBinding == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "Can't bind File to type '{0}'.", parameter.ParameterType));
            }

            IBinding binding = new FileBinding(parameter, fileAttribute, argumentBinding, path, _config);

            return Task.FromResult(binding);
        }
    }
}
