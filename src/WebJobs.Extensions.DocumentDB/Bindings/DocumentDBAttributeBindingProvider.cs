// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBAttributeBindingProvider : IBindingProvider
    {
        private JobHostConfiguration _jobHostConfig;
        private DocumentDBConfiguration _docDBConfig;

        public DocumentDBAttributeBindingProvider(JobHostConfiguration config, DocumentDBConfiguration documentDBConfig)
        {
            _jobHostConfig = config;
            _docDBConfig = documentDBConfig;
        }

        public async Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ParameterInfo parameter = context.Parameter;
            DocumentDBAttribute attribute = parameter.GetCustomAttribute<DocumentDBAttribute>(inherit: false);
            if (attribute == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(_docDBConfig.ConnectionString) &&
                string.IsNullOrEmpty(attribute.ConnectionString))
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture,
                    "The DocumentDB connection string must be set either via a '{0}' app setting, via the DocumentDBAttribute.ConnectionString property or via DocumentDBConfiguration.ConnectionString.",
                    DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName));
            }

            DocumentDBContext documentDBContext = CreateContext(_docDBConfig, attribute, _jobHostConfig.NameResolver);

            if (attribute.CreateIfNotExists)
            {
                await CreateIfNotExistAsync(documentDBContext.Service, documentDBContext.ResolvedDatabaseName, documentDBContext.ResolvedCollectionName);
            }

            Type coreType = TypeUtility.GetCoreType(parameter.ParameterType);

            IBinding binding = null;
            try
            {
                binding = BindingFactory.BindGenericCollector(parameter, typeof(DocumentDBAsyncCollector<>), coreType,
                _jobHostConfig.GetService<IConverterManager>(), (s) => documentDBContext);
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

        internal static DocumentDBContext CreateContext(DocumentDBConfiguration config, DocumentDBAttribute attribute, INameResolver resolver)
        {
            string resolvedConnectionString = config.ConnectionString;

            if (!string.IsNullOrEmpty(attribute.ConnectionString))
            {
                resolvedConnectionString = DocumentDBConfiguration.GetSettingFromConfigOrEnvironment(attribute.ConnectionString);
            }

            return new DocumentDBContext
            {
                Service = config.DocumentDBServiceFactory.CreateService(resolvedConnectionString),
                ResolvedDatabaseName = Resolve(attribute.DatabaseName, resolver),
                ResolvedCollectionName = Resolve(attribute.CollectionName, resolver)
            };
        }

        public static async Task CreateIfNotExistAsync(IDocumentDBService service, string databaseName, string documentCollectionName)
        {
            Uri databaseUri = UriFactory.CreateDatabaseUri(databaseName);
            Database database = new Database { Id = databaseName };
            DocumentCollection documentCollection = new DocumentCollection
            {
                Id = documentCollectionName
            };

            // If we queried for the Database or Collection before creation, we may hit a race condition
            // if multiple instances are running the same code. So let's just create and ignore a Conflict.
            await DocumentDBUtility.ExecuteAndIgnoreStatusCodeAsync(HttpStatusCode.Conflict,
                () => service.CreateDatabaseAsync(database));

            await DocumentDBUtility.ExecuteAndIgnoreStatusCodeAsync(HttpStatusCode.Conflict,
                () => service.CreateDocumentCollectionAsync(databaseUri, documentCollection));
        }

        private static string Resolve(string value, INameResolver resolver)
        {
            if (resolver == null)
            {
                return value;
            }

            return resolver.ResolveWholeString(value);
        }
    }
}