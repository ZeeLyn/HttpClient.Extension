using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HttpClient.Extension
{
    internal class ParameterBuilder
    {
        protected static readonly ConcurrentDictionary<Type, List<PropertyInfo>> ParametersCache =
            new ConcurrentDictionary<Type, List<PropertyInfo>>();


        internal static IReadOnlyList<PropertyInfo> GetProperties(Type type)
        { return ParametersCache.GetOrAdd(type, t => t.GetProperties().ToList()); }

        internal static Dictionary<string, string> GetAllValues(object parameters)
        {
            return GetProperties(parameters.GetType())
                .ToDictionary(key => key.Name, val => val.GetValue(parameters)?.ToString() ?? string.Empty);
        }
    }
}
