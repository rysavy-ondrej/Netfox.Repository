using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Netfox.Repository.Attributes;

namespace Netfox.Repository.Utils
{
    internal static class CachedTypeInfo
    {
        private static object _syncObject = new object();
        private static readonly Dictionary<Type, PropertyInfo[]> GetNavigableCollectionPropertiesCache =
            new Dictionary<Type, PropertyInfo[]>();

        private static readonly Dictionary<Type, PropertyInfo[]> GetNavigableReferencePropertiesCache =
            new Dictionary<Type, PropertyInfo[]>();

        private static readonly Dictionary<Type, PropertyInfo[]> GetScalarPropertiesCache =
            new Dictionary<Type, PropertyInfo[]>();

        private static readonly Dictionary<Type, PropertyInfo[]> GetComplexPropertiesCache =
            new Dictionary<Type, PropertyInfo[]>();

        public static IEnumerable<PropertyInfo> GetNavigableCollectionProperties(Type targetType)
        {
            lock (_syncObject)
            {
                PropertyInfo[] result;
                if (!GetNavigableCollectionPropertiesCache.TryGetValue(targetType, out result))
                {
                    result =
                        targetType.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance)
                            .Where(
                                property =>
                                    property.IsDefined(typeof (NCollectionAttribute), true) && property.CanRead &&
                                    property.CanWrite).ToArray();
                    GetNavigableCollectionPropertiesCache.Add(targetType, result);
                }
                return result;
            }
        }

        public static IEnumerable<PropertyInfo> GetNavigableReferenceProperties(Type targetType)
        {
            lock (_syncObject)
            {
                PropertyInfo[] result;
                if (!GetNavigableReferencePropertiesCache.TryGetValue(targetType, out result))
                {
                    result =
                        targetType.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance)
                            .Where(
                                property =>
                                    property.IsDefined(typeof (NReferenceAttribute), true) && property.CanRead &&
                                    property.CanWrite).ToArray();
                    GetNavigableReferencePropertiesCache.Add(targetType, result);
                }
                return result;
            }
        }

        public static IEnumerable<PropertyInfo> GetScalarProperties(Type targetType)
        {
            lock (_syncObject)
            {
                PropertyInfo[] result;
                if (!GetScalarPropertiesCache.TryGetValue(targetType, out result))
                {
                    result = targetType.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly |
                                                      BindingFlags.Instance)
                        .Where(
                            property =>
                                property.IsDefined(typeof (NValueAttribute), true) && property.CanRead &&
                                property.CanWrite &&
                                property.PropertyType.IsValueType).ToArray();
                    GetScalarPropertiesCache.Add(targetType, result);
                }
                return result;
            }
        }

        public static IEnumerable<PropertyInfo> GetComplexProperties(Type targetType)
        {
            lock (_syncObject)
            {
                PropertyInfo[] result;
                if (!GetComplexPropertiesCache.TryGetValue(targetType, out result))
                {
                    result =
                        targetType.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance)
                            .Where(
                                property =>
                                    property.IsDefined(typeof (NValueAttribute), true) && property.CanRead &&
                                    property.CanWrite && !property.PropertyType.IsValueType).ToArray();
                    GetComplexPropertiesCache.Add(targetType, result);
                }
                return result;
            }
        }

        public static IEnumerable<TypeInfo> GetDocumentTypes(Assembly assembly)
        {
            return assembly.DefinedTypes.Where(ti => ti.IsDefined(typeof (NDocumentAttribute), true));
        }
    }
}