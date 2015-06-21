using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Common.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Common.Converters;
using Microsoft.Azure.WebJobs.Extensions.Files.Converters;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Converters;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    /// <summary>
    /// Binds a parameter to a file.
    /// </summary>
    internal class FileBinding : IBinding
    {
        private readonly FilesConfiguration _config;
        private readonly ParameterInfo _parameter;
        private IArgumentBinding<FileBindingInfo> _argumentBinding;
        private IObjectToTypeConverter<FileInfo> _converter;
        private BindablePath _path;
        private FileAttribute _attribute;

        /// <summary>
        /// Constructs a new instance
        /// </summary>
        /// <param name="parameter">The parameter to bind to.</param>
        /// <param name="attribute">The <see cref="FileAttribute"/> applied to the parameter.</param>
        /// <param name="argumentBinding">The argument binding for the parameter.</param>
        /// <param name="path">The <see cref="BindablePath"/> for the parameter.</param>
        /// <param name="config">The <see cref="FilesConfiguration"/></param>
        public FileBinding(ParameterInfo parameter, FileAttribute attribute, IArgumentBinding<FileBindingInfo> argumentBinding, BindablePath path, FilesConfiguration config)
        {
            _parameter = parameter;
            _attribute = attribute;
            _argumentBinding = argumentBinding;
            _path = path;
            _config = config;
            _converter = CreateConverter();
        }

        public bool FromAttribute
        {
            get
            {
                return true;
            }
        }

        public async Task<IValueProvider> BindAsync(BindingContext context)
        {
            string boundFileName = _path.Bind(context.BindingData);
            string filePath = Path.Combine(_config.RootPath, boundFileName);

            FileBindingInfo bindingInfo = new FileBindingInfo
            {
                Attribute = _attribute,
                FileInfo = new FileInfo(filePath)
            };

            return await BindAsync(bindingInfo, context.ValueContext);
        }

        public async Task<IValueProvider> BindAsync(object value, ValueBindingContext context)
        {
            FileInfo fileInfo = null;
         
            if (!_converter.TryConvert(value, out fileInfo))
            {
                throw new InvalidOperationException("Unable to convert value to FileInfo.");
            }

            return await BindAsync(fileInfo, context);
        }

        private Task<IValueProvider> BindAsync(FileBindingInfo bindingInfo, ValueBindingContext context)
        {
            return _argumentBinding.BindAsync(bindingInfo, context);
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor
            {
                Name = _parameter.Name,
                DisplayHints = new ParameterDisplayHints
                {
                    // TODO: Finish Dashboard integration
                }
            };
        }

        private static IObjectToTypeConverter<FileInfo> CreateConverter()
        {
            return new CompositeObjectToTypeConverter<FileInfo>(
                new OutputConverter<FileInfo, FileInfo>(new IdentityConverter<FileInfo>()),
                new OutputConverter<string, FileInfo>(new StringToFileInfoConverter()));
        }
    }
}
