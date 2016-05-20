// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.DocumentDB
{
    internal class DocumentDBItemValueBinder<T> : IValueBinder where T : class
    {
        private DocumentDBContext _context;
        private JObject _originalItem;

        public DocumentDBItemValueBinder(DocumentDBContext context)
        {
            _context = context;
        }

        public Type Type
        {
            get { return typeof(T); }
        }

        public async Task SetValueAsync(object value, CancellationToken cancellationToken)
        {
            if (value == null || _originalItem == null)
            {
                return;
            }

            await SetValueInternalAsync(_originalItem, value as T, _context);
        }

        public object GetValue()
        {
            Uri documentUri = UriFactory.CreateDocumentUri(_context.ResolvedAttribute.DatabaseName, _context.ResolvedAttribute.CollectionName, _context.ResolvedAttribute.Id);
            RequestOptions options = null;

            if (!string.IsNullOrEmpty(_context.ResolvedAttribute.PartitionKey))
            {
                options = new RequestOptions
                {
                    PartitionKey = new PartitionKey(_context.ResolvedAttribute.PartitionKey)
                };
            }

            T document = DocumentDBUtility.RetryAsync(() => _context.Service.ReadDocumentAsync<T>(documentUri, options),
                _context.MaxThrottleRetries, codesToIgnore: HttpStatusCode.NotFound).Result;

            if (document != null)
            {
                _originalItem = JObject.FromObject(document);
            }

            return document;
        }

        public string ToInvokeString()
        {
            return string.Empty;
        }

        internal static async Task SetValueInternalAsync(JObject originalItem, T newItem, DocumentDBContext context)
        {
            JObject currentValue = JObject.FromObject(newItem);

            if (HasChanged(originalItem, currentValue))
            {
                // make sure it's not the id that has changed
                string originalId = null;
                string currentId = null;
                if (TryGetId(currentValue, out currentId) &&
                    !string.IsNullOrEmpty(currentId) &&
                    TryGetId(originalItem, out originalId) &&
                    !string.IsNullOrEmpty(originalId))
                {
                    // make sure it's not the Id that has changed
                    if (!string.Equals(originalId, currentId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Cannot update the 'Id' property.");
                    }
                }
                else
                {
                    // If the serialzied object does not have a lowercase 'id' property, DocDB will reject it.
                    // We'll just short-circuit here since we validate that the 'id' hasn't changed.
                    throw new InvalidOperationException(string.Format("The document must have an 'id' property."));
                }

                Uri documentUri = UriFactory.CreateDocumentUri(context.ResolvedAttribute.DatabaseName, context.ResolvedAttribute.CollectionName, originalId);
                await DocumentDBUtility.RetryAsync(() => context.Service.ReplaceDocumentAsync(documentUri, newItem),
                    context.MaxThrottleRetries);
            }
        }

        internal static bool TryGetId(JObject item, out string id)
        {
            id = null;
            JToken idToken = null;

            // 'id' must be lowercase
            if (item.TryGetValue("id", StringComparison.Ordinal, out idToken))
            {
                id = idToken.ToString();
                return true;
            }

            return false;
        }

        internal static bool HasChanged(JToken original, JToken current)
        {
            return !JToken.DeepEquals(original, current);
        }

        internal static JObject CloneItem(object item)
        {
            string serializedItem = JsonConvert.SerializeObject(item);
            return JObject.Parse(serializedItem);
        }
    }
}