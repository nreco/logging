using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace NReco.Logging.File
{
    /// <summary>
    /// Create and cache compiled expression to fill the dictionary from an object
    /// </summary>
    /// <remarks>
    /// https://www.meziantou.net/asp-net-core-json-logger.htm
    /// </remarks>
    internal static class ExpressionCache
    {
        public delegate void AppendToDictionary(IDictionary<string, object> dictionary, object o);

        private static readonly ConcurrentDictionary<Type, AppendToDictionary> s_typeCache = new ConcurrentDictionary<Type, AppendToDictionary>();
        private static readonly PropertyInfo _dictionaryIndexerProperty = GetDictionaryIndexer();

        public static AppendToDictionary GetOrCreateAppendToDictionaryMethod(Type type)
        {
            return s_typeCache.GetOrAdd(type, t => CreateAppendToDictionaryMethod(t));
        }

        private static AppendToDictionary CreateAppendToDictionaryMethod(Type type)
        {
            var dictionaryParameter = Expression.Parameter(typeof(IDictionary<string, object>), "dictionary");
            var objectParameter = Expression.Parameter(typeof(object), "o");

            var castedParameter = Expression.Convert(objectParameter, type); // cast o to the actual type
            // Create setter for each properties
            // dictionary["PropertyName"] = o.PropertyName;
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            var setters =
                from prop in properties
                where prop.CanRead
                let indexerExpression = Expression.Property(dictionaryParameter, _dictionaryIndexerProperty, Expression.Constant(prop.Name))
                let getExpression = Expression.Property(castedParameter, prop.GetMethod)
                select Expression.Assign(indexerExpression, getExpression);

            var body = new List<Expression>(properties.Length + 1)
                {
                    castedParameter
                };
            body.AddRange(setters);

            var lambdaExpression = Expression.Lambda<AppendToDictionary>(Expression.Block(body), dictionaryParameter, objectParameter);
            return lambdaExpression.Compile();
        }

        // Get the PropertyInfo for IDictionary<string, object>.this[string key]
        private static PropertyInfo GetDictionaryIndexer()
        {
            var indexers = from prop in typeof(IDictionary<string, object>).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                           let indexParameters = prop.GetIndexParameters()
                           where indexParameters.Length == 1 && typeof(string).IsAssignableFrom(indexParameters[0].ParameterType)
                           select prop;

            return indexers.Single();
        }
    }
}
