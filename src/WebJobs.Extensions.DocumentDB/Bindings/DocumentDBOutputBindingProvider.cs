// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
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

        public async Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;

            if (IsTypeValid(parameter.ParameterType))
            {
                if (_context.CreateIfNotExists)
                {
                    await CreateIfNotExistAsync(_context.Service, _context.ResolvedDatabaseName,
                        _context.ResolvedCollectionName, _context.ResolvedPartitionKey, _context.CollectionThroughput);
                }

                return CreateBinding(parameter, _context, _converterManager);
            }

            return null;
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

        internal static async Task CreateIfNotExistAsync(IDocumentDBService service, string databaseName,
         string documentCollectionName, string partitionKeyPath, int throughput)
        {
            Uri databaseUri = UriFactory.CreateDatabaseUri(databaseName);
            Database database = new Database { Id = databaseName };

            DocumentCollection documentCollection = new DocumentCollection
            {
                Id = documentCollectionName
            };

            if (!string.IsNullOrEmpty(partitionKeyPath))
            {
                documentCollection.PartitionKey.Paths.Add(partitionKeyPath);
            }

            // If there is any throughput specified, pass it on. DocumentClient will throw with a 
            // descriptive message if the value does not meet the collection requirements.
            RequestOptions collectionOptions = null;
            if (throughput != 0)
            {
                collectionOptions = new RequestOptions
                {
                    OfferThroughput = throughput
                };
            }

            // If we queried for the Database or Collection before creation, we may hit a race condition
            // if multiple instances are running the same code. So let's just create and ignore a Conflict.
            await DocumentDBUtility.ExecuteAndIgnoreStatusCodeAsync(HttpStatusCode.Conflict,
                    () => service.CreateDatabaseAsync(database));

            await DocumentDBUtility.ExecuteAndIgnoreStatusCodeAsync(HttpStatusCode.Conflict,
                () => service.CreateDocumentCollectionAsync(databaseUri, documentCollection, collectionOptions));
        }
    }
}
