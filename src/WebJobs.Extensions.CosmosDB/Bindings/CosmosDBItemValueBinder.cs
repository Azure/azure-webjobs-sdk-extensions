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

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB
{
    internal class CosmosDBItemValueBinder<T> : IValueBinder
        where T : class
    {
        private CosmosDBContext _context;
        private JObject _originalItem;

        public CosmosDBItemValueBinder(CosmosDBContext context)
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

        public async Task<object> GetValueAsync()
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

            Document document = null;

            try
            {
                document = await _context.Service.ReadDocumentAsync(documentUri, options).ConfigureAwait(false);
            }
            catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // ignore not found; we'll return null below
            }

            if (document == null)
            {
                return document;
            }

            T item = null;

            // Strings need to be handled differently.
            if (typeof(T) == typeof(string))
            {
                _originalItem = JObject.FromObject(document);
                item = _originalItem.ToString(Formatting.None) as T;
            }
            else
            {
                item = (T)(dynamic)document;
                _originalItem = JObject.FromObject(item);
            }

            return item;
        }

        public string ToInvokeString()
        {
            return string.Empty;
        }

        internal static async Task SetValueInternalAsync(JObject originalItem, T newItem, CosmosDBContext context)
        {
            // We can short-circuit here as strings are immutable.
            if (newItem is string)
            {
                return;
            }

            JObject currentValue = JObject.FromObject(newItem);

            if (HasChanged(originalItem, currentValue))
            {
                // make sure it's not the id that has changed
                if (TryGetId(currentValue, out string currentId) &&
                    !string.IsNullOrEmpty(currentId) &&
                    TryGetId(originalItem, out string originalId) &&
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
                await context.Service.ReplaceDocumentAsync(documentUri, newItem).ConfigureAwait(false);
            }
        }

        internal static bool TryGetId(JObject item, out string id)
        {
            id = null;

            // 'id' must be lowercase
            if (item.TryGetValue("id", StringComparison.Ordinal, out JToken idToken))
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