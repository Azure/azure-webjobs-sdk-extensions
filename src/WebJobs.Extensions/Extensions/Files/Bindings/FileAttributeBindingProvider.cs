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
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;

namespace Microsoft.Azure.WebJobs.Extensions.Files.Bindings
{
    internal class FileAttributeBindingProvider : IBindingProvider
    {
        private readonly FilesConfiguration _config;
        private readonly INameResolver _nameResolver;

        public FileAttributeBindingProvider(FilesConfiguration config, INameResolver nameResolver)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (nameResolver == null)
            {
                throw new ArgumentNullException("nameResolver");
            }

            _config = config;
            _nameResolver = nameResolver;
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

            // first, verify the file path binding (if it contains binding parameters)
            string path = attribute.Path;
            if (_nameResolver != null)
            {
                path = _nameResolver.ResolveWholeString(path);
            }
            BindingTemplate bindingTemplate = BindingTemplate.FromString(path);
            bindingTemplate.ValidateContractCompatibility(context.BindingDataContract);

            IEnumerable<Type> types = StreamValueBinder.GetSupportedTypes(attribute.Access)
                .Union(new Type[] { typeof(FileStream), typeof(FileInfo) });
            if (!ValueBinder.MatchParameterType(context.Parameter, types))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, 
                    "Can't bind FileAttribute to type '{0}'.", parameter.ParameterType));
            }

            return Task.FromResult<IBinding>(new FileBinding(_config, parameter, bindingTemplate));
        }
    }
}
