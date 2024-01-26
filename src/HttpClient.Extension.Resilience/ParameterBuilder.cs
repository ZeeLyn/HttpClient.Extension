using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace HttpClient.Extension.Resilience
{
    public class ParameterBuilder
    {
        protected static readonly ConcurrentDictionary<Type, List<PropertyInfo>> ParametersCache = new();


        internal static IReadOnlyList<PropertyInfo> GetProperties(Type type)
        {
            return ParametersCache.GetOrAdd(type, t => t.GetProperties().ToList());
        }

        internal static string BuildQuery(IDictionary<string, string> parameters)
        {
            return BuildQueryString(parameters);
        }

        internal static string BuildQuery(object parameters)
        {
            return BuildQueryString(GetProperties(parameters.GetType()), parameters);
        }

        internal static Dictionary<string, string> GetAllValues(object parameters)
        {
            return GetProperties(parameters.GetType())
                .ToDictionary(key => key.Name, val => val.GetValue(parameters)?.ToString());
        }

        private static string BuildQueryString(IDictionary<string, string> parameters)
        {
            var builder = new StringBuilder();
            foreach (var item in parameters)
            {
                builder.AppendFormat("&{0}={1}", item.Key, item.Value);
            }

            return builder.ToString().TrimStart('&');
        }

        private static string BuildQueryString(IReadOnlyList<PropertyInfo> properties, object obj)
        {
            var builder = new StringBuilder();
            foreach (var item in properties)
            {
                builder.AppendFormat("&{0}={1}", item.Name, item.GetValue(obj));
            }

            return builder.ToString().TrimStart('&');
        }
    }
}