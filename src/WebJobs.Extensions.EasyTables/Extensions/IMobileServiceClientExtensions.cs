// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Microsoft.WindowsAzure.MobileServices
{
    internal static class IMobileServiceClientExtensions
    {
        private const string SerializerPropertyName = "Serializer";
        private const string SerializerSettingsPropertyName = "SerializerSettings";
        private const string TableNameCacheFieldName = "tableNameCache";

        // Specifying TableName on the attribute overrides any table name inferred by the object's type. So if a 
        // TableName has been set, we have to do some private reflection to update the internal tableNameCache.
        // We will address this in the Mobile Services code to make this scenario supported and then remove this
        // private reflection.
        public static void AddToTableNameCache(this IMobileServiceClient client, Type type, string tableName)
        {
            string clientTypeName = client.GetType().Name;

            // Get Serializer
            PropertyInfo serializerProperty = client.GetType().GetProperty(SerializerPropertyName, BindingFlags.NonPublic | BindingFlags.Instance);
            ThrowIfNullMemberInfo(serializerProperty, clientTypeName, clientTypeName, SerializerPropertyName);

            object serializer = serializerProperty.GetValue(client);
            ThrowIfNullValue(serializer, clientTypeName, clientTypeName, SerializerPropertyName);

            // Get SerializerSettings
            PropertyInfo settingsProperty = serializer.GetType().GetProperty(SerializerSettingsPropertyName, BindingFlags.Public | BindingFlags.Instance);
            ThrowIfNullMemberInfo(settingsProperty, clientTypeName, serializer.GetType().Name, SerializerSettingsPropertyName);

            MobileServiceJsonSerializerSettings settings = settingsProperty.GetValue(serializer) as MobileServiceJsonSerializerSettings;
            ThrowIfNullValue(settings, clientTypeName, serializer.GetType().Name, SerializerSettingsPropertyName);

            // Get cache
            FieldInfo tableNameCacheField = settings.ContractResolver.GetType().GetField(TableNameCacheFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            ThrowIfNullMemberInfo(tableNameCacheField, clientTypeName, settings.ContractResolver.GetType().Name, TableNameCacheFieldName);

            Dictionary<Type, string> tableNameCache = tableNameCacheField.GetValue(settings.ContractResolver) as Dictionary<Type, string>;
            ThrowIfNullValue(tableNameCache, clientTypeName, settings.ContractResolver.GetType().Name, TableNameCacheFieldName);

            // Update cache
            tableNameCache[type] = tableName;
        }

        private static void ThrowIfNullMemberInfo(MemberInfo memberInfo, string clientTypeName, string classTypeName, string memberName)
        {
            ThrowIfNull(memberInfo, clientTypeName, classTypeName, memberName, "was not found");
        }

        private static void ThrowIfNullValue(object value, string clientTypeName, string classTypeName, string memberName)
        {
            ThrowIfNull(value, clientTypeName, classTypeName, memberName, "was null");
        }

        private static void ThrowIfNull(object value, string clientTypeName, string classTypeName, string memberName, string description)
        {
            if (value == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "Incompatible implementation of {0}. The internal member '{1}' on the type '{2}' {3}.",
                    clientTypeName, memberName, classTypeName, description));
            }
        }
    }
}
