using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace HttpClient.Extension
{
    internal class ParameterBuilder
    {
        protected static readonly ConcurrentDictionary<Type, List<PropertyInfo>> ParametersCache =
            new ConcurrentDictionary<Type, List<PropertyInfo>>();


        internal static IReadOnlyList<PropertyInfo> GetProperties(Type type)
        {
            return ParametersCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetMethod != null && p.GetMethod.IsPublic && p.GetIndexParameters().Length == 0).ToList());
        }

        internal static IDictionary<string, string> GetAllValues(object parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            if (parameters is IDictionary<string, string> dictionary)
                return dictionary;
            return GetProperties(parameters.GetType())
                .ToDictionary(key => key.Name, val => Convert.ToString(val.GetValue(parameters), CultureInfo.InvariantCulture) ?? string.Empty);
        }
    }
}
