// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBOutputBindingProvider : IBindingProvider
    {
        private IConverterManager _converterManager;
        private DocumentDBContext _context;

        public DocumentDBOutputBindingProvider(DocumentDBContext context, IConverterManager converterManager)
        {
            _context = context;
            _converterManager = converterManager;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            IBinding binding = null;

            if (IsTypeValid(parameter.ParameterType))
            {
                binding = CreateBinding(parameter, _context, _converterManager);
            }

            return Task.FromResult(binding);
        }

        internal static bool IsTypeValid(Type type)
        {
            // For output bindings, all types are valid. If DocumentDB can't handle it, it'll throw.
            bool isValidOut = TypeUtility.IsValidOutType(type, (t) => true);
            bool isValidCollector = TypeUtility.IsValidCollectorType(type, (t) => true);

            return isValidOut || isValidCollector;
        }

        internal static IBinding CreateBinding(ParameterInfo parameter, DocumentDBContext context, IConverterManager converterManager)
        {
            IBinding binding = null;
            Type coreType = TypeUtility.GetCoreType(parameter.ParameterType);
            try
            {
                binding = BindingFactory.BindGenericCollector(parameter, typeof(DocumentDBAsyncCollector<>), coreType,
                    converterManager, (s) => context);
            }
            catch (Exception ex)
            {
                // BindingFactory uses a couple levels of reflection so we get TargetInvocationExceptions here.
                // This pulls out the root exception and rethrows it.
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }

                ExceptionDispatchInfo.Capture(ex).Throw();
            }
            return binding;
        }
    }
}
