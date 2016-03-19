// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.EasyTables
{
    internal class EasyTableTestHelper
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
            return typeof(EasyTableTestHelper)
                .GetMethod("OutputParameters", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters();
        }

        public static IEnumerable<ParameterInfo> GetValidInputItemParameters()
        {
            return typeof(EasyTableTestHelper)
               .GetMethod("InputItemParameters", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters();
        }

        public static ParameterInfo GetInputParameter<T>()
        {
            return GetValidInputItemParameters().Where(p => p.ParameterType == typeof(T)).Single();
        }

        public static IEnumerable<ParameterInfo> GetValidInputTableParameters()
        {
            return typeof(EasyTableTestHelper)
               .GetMethod("InputTableParameters", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters();
        }

        public static IEnumerable<ParameterInfo> GetValidInputQueryParameters()
        {
            return typeof(EasyTableTestHelper)
               .GetMethod("InputQueryParameters", BindingFlags.Instance | BindingFlags.NonPublic).GetParameters();
        }

        private void OutputParameters(
            [EasyTable(TableName = "Items")] out JObject jobjectOut,
            [EasyTable] out TodoItem pocoOut,
            [EasyTable(TableName = "Items")] out JObject[] jobjectArrayOut,
            [EasyTable] out TodoItem[] pocoArrayOut,
            [EasyTable(TableName = "Items")] IAsyncCollector<JObject> jobjectAsyncCollector,
            [EasyTable] IAsyncCollector<TodoItem> pocoAsyncCollector,
            [EasyTable(TableName = "Items")] ICollector<JObject> jobjectCollector,
            [EasyTable] ICollector<TodoItem> pocoCollector,
            [EasyTable(TableName = "Item")] out object objectOut,
            [EasyTable(TableName = "Item")] ICollector<object> objectCollector)
        {
            jobjectOut = null;
            pocoOut = null;
            jobjectArrayOut = null;
            pocoArrayOut = null;
            objectOut = null;
        }

        private void InputItemParameters(
            [EasyTable(TableName = "Items")] JObject jobject,
            [EasyTable] TodoItem poco)
        {
        }

        private void InputTableParameters(
            [EasyTable(TableName = "Items")] IMobileServiceTable jobjectTable,
            [EasyTable] IMobileServiceTable<TodoItem> pocoTable)
        {
        }

        private void InputQueryParameters(
            [EasyTable] IMobileServiceTableQuery<TodoItem> query)
        {
        }
    }
}