// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps
{
    internal class MobileAppTestHelper
    {
        public static IEnumerable<ParameterInfo> GetAllValidParameters()
        {
            var outputParams = GetValidOutputParameters();
            var inputItemParams = GetValidInputItemParameters();
            var inputTableParams = GetValidInputTableParameters();
            var inputQueryParams = GetValidInputQueryParameters();

            return outputParams.Concat(inputItemParams.Concat(inputTableParams.Concat(inputQueryParams)));
        }

        public static IEnumerable<ParameterInfo> GetValidOutputParameters()
        {
            return typeof(MobileAppTestHelper)
                .GetMethod("OutputParameters", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters();
        }

        public static IEnumerable<ParameterInfo> GetValidInputItemParameters()
        {
            return typeof(MobileAppTestHelper)
               .GetMethod("InputItemParameters", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters();
        }

        public static ParameterInfo GetInputParameter<T>()
        {
            return GetValidInputItemParameters().Where(p => p.ParameterType == typeof(T)).Single();
        }

        public static IEnumerable<ParameterInfo> GetValidInputTableParameters()
        {
            return typeof(MobileAppTestHelper)
               .GetMethod("InputTableParameters", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters();
        }

        public static IEnumerable<ParameterInfo> GetValidInputQueryParameters()
        {
            return typeof(MobileAppTestHelper)
               .GetMethod("InputQueryParameters", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters();
        }

        private void OutputParameters(
            [MobileTable(TableName = "Items")] out JObject jobjectOut,
            [MobileTable] out TodoItem pocoOut,
            [MobileTable(TableName = "Items")] out JObject[] jobjectArrayOut,
            [MobileTable] out TodoItem[] pocoArrayOut,
            [MobileTable(TableName = "Items")] IAsyncCollector<JObject> jobjectAsyncCollector,
            [MobileTable] IAsyncCollector<TodoItem> pocoAsyncCollector,
            [MobileTable(TableName = "Items")] ICollector<JObject> jobjectCollector,
            [MobileTable] ICollector<TodoItem> pocoCollector,
            [MobileTable(TableName = "Item")] out object objectOut,
            [MobileTable(TableName = "Item")] ICollector<object> objectCollector)
        {
            jobjectOut = null;
            pocoOut = null;
            jobjectArrayOut = null;
            pocoArrayOut = null;
            objectOut = null;
        }

        private void InputItemParameters(
            [MobileTable(TableName = "Items")] JObject jobject,
            [MobileTable] TodoItem poco)
        {
        }

        private void InputTableParameters(
            [MobileTable(TableName = "Items")] IMobileServiceTable jobjectTable,
            [MobileTable] IMobileServiceTable<TodoItem> pocoTable)
        {
        }

        private void InputQueryParameters(
            [MobileTable] IMobileServiceTableQuery<TodoItem> query)
        {
        }
    }
}