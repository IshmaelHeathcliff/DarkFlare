using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DarkFlare.Editor
{
    public static class ConfigurationTypeDiscovery
    {
        public const string ConfigMenuPrefix = "DarkFlare/Data/";

        const BindingFlags SerializedFieldFlags = BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic;

        public static IReadOnlyList<Type> FindTopLevelTypes()
        {
            return TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(IsCreatableConfigType)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<Type> FindNestedSerializedTypes(IReadOnlyList<Type> topLevelTypes)
        {
            HashSet<Type> visited = new HashSet<Type>();
            Queue<Type> pending = new Queue<Type>();
            List<Type> result = new List<Type>();

            if (topLevelTypes == null)
            {
                return result;
            }

            for (int i = 0; i < topLevelTypes.Count; i++)
            {
                Type type = topLevelTypes[i];

                if (type != null && visited.Add(type))
                {
                    pending.Enqueue(type);
                }
            }

            while (pending.Count > 0)
            {
                Type ownerType = pending.Dequeue();
                IReadOnlyList<FieldInfo> fields = FindSerializedFields(ownerType);

                for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    Type candidate = UnwrapSerializedElementType(fields[fieldIndex].FieldType);

                    if (!IsProjectNestedSerializedType(candidate) || !visited.Add(candidate))
                    {
                        continue;
                    }

                    result.Add(candidate);
                    pending.Enqueue(candidate);
                }
            }

            return result
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<FieldInfo> FindSerializedFields(Type type)
        {
            List<FieldInfo> result = new List<FieldInfo>();

            for (Type current = type;
                 current != null && current != typeof(ScriptableObject) && current != typeof(UnityEngine.Object);
                 current = current.BaseType)
            {
                FieldInfo[] fields = current.GetFields(SerializedFieldFlags | BindingFlags.DeclaredOnly);

                for (int i = 0; i < fields.Length; i++)
                {
                    if (IsUnitySerializedField(fields[i]))
                    {
                        result.Add(fields[i]);
                    }
                }
            }

            return result
                .OrderBy(field => field.MetadataToken)
                .ToArray();
        }

        public static int CountSerializedFields(IEnumerable<Type> types)
        {
            if (types == null)
            {
                return 0;
            }

            int count = 0;

            foreach (Type type in types)
            {
                count += FindSerializedFields(type).Count;
            }

            return count;
        }

        public static bool IsCreatableConfigType(Type type)
        {
            if (type == null || type.IsAbstract || !typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return false;
            }

            CreateAssetMenuAttribute createMenu = type.GetCustomAttribute<CreateAssetMenuAttribute>();

            return createMenu != null
                && !string.IsNullOrWhiteSpace(createMenu.menuName)
                && createMenu.menuName.StartsWith(ConfigMenuPrefix, StringComparison.Ordinal);
        }

        static bool IsUnitySerializedField(FieldInfo field)
        {
            if (field == null
                || field.IsStatic
                || field.IsInitOnly
                || field.IsLiteral
                || field.IsDefined(typeof(NonSerializedAttribute), true))
            {
                return false;
            }

            return field.IsPublic
                || field.IsDefined(typeof(SerializeField), true)
                || field.IsDefined(typeof(SerializeReference), true);
        }

        static Type UnwrapSerializedElementType(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return type.GetGenericArguments()[0];
            }

            return type;
        }

        static bool IsProjectNestedSerializedType(Type type)
        {
            return type != null
                && !type.IsPrimitive
                && !type.IsEnum
                && type != typeof(string)
                && string.Equals(type.Namespace, "DarkFlare", StringComparison.Ordinal)
                && !typeof(UnityEngine.Object).IsAssignableFrom(type)
                && type.IsDefined(typeof(SerializableAttribute), false);
        }
    }
}
