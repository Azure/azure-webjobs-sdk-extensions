// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Bindings;
using Microsoft.Azure.WebJobs.Extensions.Files.Listener;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal class FileTriggerAttributeBindingProvider : ITriggerBindingProvider
    {
        private readonly IOptions<FilesOptions> _options;
        private readonly ILogger _logger;
        private readonly IFileProcessorFactory _fileProcessorFactory;

        public FileTriggerAttributeBindingProvider(IOptions<FilesOptions> options, ILoggerFactory loggerFactory, IFileProcessorFactory fileProcessorFactory)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = loggerFactory?.CreateLogger(LogCategories.CreateTriggerCategory("File"));
            _fileProcessorFactory = fileProcessorFactory ?? throw new ArgumentNullException(nameof(fileProcessorFactory));
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
            IEnumerable<Type> types = StreamValueBinder.GetSupportedTypes(FileAccess.Read)
                .Union(new Type[] { typeof(FileStream), typeof(FileSystemEventArgs), typeof(FileInfo) });
            if (!ValueBinder.MatchParameterType(context.Parameter, types))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    "Can't bind FileTriggerAttribute to type '{0}'.", parameter.ParameterType));
            }

            return Task.FromResult<ITriggerBinding>(new FileTriggerBinding(_options, parameter, _logger, _fileProcessorFactory));
        }
    }
}
