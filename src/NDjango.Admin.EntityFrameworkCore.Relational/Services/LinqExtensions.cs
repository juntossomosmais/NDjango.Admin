using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NDjango.Admin.EntityFrameworkCore;
using Exp = System.Linq.Expressions.Expression;

namespace NDjango.Admin.Services
{

    /// <summary>
    /// Contains options for full text search
    /// </summary>
    public class FullTextSearchOptions
    {

        /// <summary>
        /// Lamda expression, which filters properties to use in full text search
        /// </summary>
        public Func<PropertyInfo, bool> Filter { get; set; }

        /// <summary>
        /// Filters properties by their <em>path</em> from the root entity — <c>"Name"</c> at the
        /// top level, <c>"User.Name"</c> one relationship in.
        /// </summary>
        /// <remarks>
        /// <see cref="Filter"/> receives a <see cref="PropertyInfo"/> and nothing else, so it
        /// cannot tell <c>Name</c> on the root from <c>Name</c> on a related entity. That is
        /// harmless while <see cref="Depth"/> is zero and wrong as soon as it is not.
        /// <para>
        /// When set it takes precedence over <see cref="Filter"/>. It is also consulted for the
        /// navigation properties themselves, which is what decides whether the search descends
        /// into one — refusing a relationship nobody asked about is what keeps a deeper search
        /// from becoming a search over the whole object graph.
        /// </para>
        /// </remarks>
        public Func<string, PropertyInfo, bool>? PathFilter { get; set; }

        /// <summary>
        /// The path already walked, as a prefix ending in a dot, or empty at the root.
        /// </summary>
        /// <remarks>
        /// Carried on the options rather than passed down, for the same reason <see cref="Depth"/>
        /// is: the recursion adjusts it on the way in and restores it on the way out, and keeping
        /// both in one place makes them impossible to get out of step.
        /// </remarks>
        internal string CurrentPath { get; set; } = string.Empty;

        /// <summary>
        /// The name of the property to order by the result list
        /// </summary>
        public string OrderBy { get; set; }

        /// <summary>
        /// if set to <c>true</c> then we use descending order
        /// </summary>
        public bool IsDescendingOrder { get; set; } = false;

        /// <summary>
        /// Depth of full text search. 
        /// </summary>
        public int Depth { get; set; } = 0;
    }

    public static class LinqExtensions
    {
        /// <summary>
        /// Filters a sequence of values based on a fulltext search predicate 
        /// </summary>
        /// <typeparam name="T">Any type</typeparam>
        /// <param name="source">The source - some IQueryable object.</param>
        /// <param name="text">The text - meaning of the search</param>
        /// <param name="options">The options for full-text search</param>
        /// <returns></returns>
        public static IQueryable<T> FullTextSearchQuery<T>(this IQueryable<T> source, string text, FullTextSearchOptions options = null)
        {
            var pe = Exp.Parameter(typeof(T), "d");
            var predicateBody = CreateSubExpression(pe, typeof(T), text, options, isQueriable: true);

            if (predicateBody != null) {
                var whereExp = Exp.Lambda<Func<T, bool>>(predicateBody, new ParameterExpression[] { pe });
                source = source.Where(whereExp);
            }

            //Order by Expression
            if (options == null) {
                return source;
            }

            var orderByProperty = options.OrderBy;
            if (string.IsNullOrEmpty(orderByProperty)) {
                orderByProperty = typeof(T).GetProperties().First().Name;
            }

            var property = Exp.Property(pe, orderByProperty);
            var lambda = Exp.Lambda(property, pe);
            var method = options.IsDescendingOrder ? "OrderByDescending" : "OrderBy";
            var orderByExp = Exp.Call(typeof(Queryable), method, new[] { typeof(T), property.Type }, source.Expression, lambda);
            source = source.Provider.CreateQuery<T>(orderByExp) as IOrderedQueryable<T>;
            return source;
        }

        private static Exp CreateSubExpression(Exp expr, Type type, string text, FullTextSearchOptions options, bool isQueriable = false)
        {
            var texts = text.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower())
                    .Where(t => t.Length > 0)
                    .ToList();

            var properties = type.GetProperties().AsEnumerable();
            if (isQueriable) {
                properties = properties.GetMappedProperties();
            }

            Exp predicateBody = null;
            foreach (var prop in properties) {
                // The path this property has from the root entity: "Name" at the top level,
                // "User.Name" one relationship in. It is what lets a caller say which of two
                // identically named columns it meant.
                var path = (options?.CurrentPath ?? string.Empty) + prop.Name;

                //Check if we can use this property in search
                if (Accepts(options, path, prop)) {
                    var paramExp = Exp.Property(expr, prop);

                    if (prop.PropertyType.IsNumeric() || prop.PropertyType == typeof(string)) {
                        Exp notNullExp = null;
                        Exp toStringExp = paramExp;

                        if (prop.PropertyType == typeof(string)) {
                            Exp nullConst = Exp.Constant(null, typeof(string));
                            notNullExp = Exp.NotEqual(paramExp, nullConst);
                        }
                        else {//property is a numeric
                            if (!isQueriable) {
                                Exp toNullableExp = null;

                                Exp valueExp = null;
                                if (!prop.PropertyType.IsNullable()) {
                                    var nullableType = typeof(Nullable<>).MakeGenericType(prop.PropertyType);
                                    toNullableExp = Exp.TypeAs(paramExp, nullableType);
                                    notNullExp = Exp.Property(toNullableExp, "HasValue");
                                    valueExp = Exp.Property(toNullableExp, "Value");
                                }
                                else {
                                    notNullExp = Exp.Property(paramExp, "HasValue");
                                    valueExp = Exp.Property(paramExp, "Value");
                                }

                                notNullExp = Exp.Equal(notNullExp, Exp.Constant(true, typeof(bool)));

                                var convertToString = typeof(Convert).GetMethod("ToString", new[] { prop.PropertyType });
                                toStringExp = Exp.Call(convertToString, valueExp);
                            }
                            else {
                                var convertToString = typeof(Convert).GetMethod("ToString", new[] { prop.PropertyType });
                                toStringExp = Exp.Call(convertToString, Exp.Convert(paramExp, prop.PropertyType));
                            }
                        }

                        Exp peLower = Exp.Call(toStringExp, typeof(string).GetMethod("ToLower", System.Type.EmptyTypes));

                        Exp conditionExp = null;
                        foreach (var txt in texts) {
                            var constant = Exp.Constant(txt, typeof(string));
                            Exp containsExp = Exp.Call(peLower, typeof(string).GetMethod("Contains", new[] { typeof(string) }), constant);
                            conditionExp = conditionExp != null ? Exp.OrElse(conditionExp, containsExp) : containsExp;
                        }

                        var andExp = (notNullExp != null) ? Exp.AndAlso(notNullExp, conditionExp) : conditionExp;
                        predicateBody = predicateBody != null ? Exp.OrElse(predicateBody, andExp) : andExp;
                    }

                    //If this property is't simple and the depth > 0
                    if (options != null && options.Depth != 0
                        && !prop.PropertyType.IsSimpleType() && !prop.PropertyType.IsEnumerable()) {
                        var outerPath = options.CurrentPath;
                        options.Depth -= 1;
                        options.CurrentPath = path + ".";
                        var subExp = CreateSubExpression(paramExp, prop.PropertyType, text, options, isQueriable);
                        options.CurrentPath = outerPath;
                        options.Depth += 1;

                        if (subExp != null) {
                            Exp notNullExp = Exp.NotEqual(paramExp, Exp.Constant(null, typeof(object)));
                            subExp = Exp.AndAlso(notNullExp, subExp);
                            predicateBody = (predicateBody != null) ? Exp.OrElse(predicateBody, subExp) : subExp;
                        }
                    }

                }
            }

            return predicateBody;
        }

        /// <summary>
        /// Whether a property takes part in the search: by path when the caller filters that way,
        /// by <see cref="FullTextSearchOptions.Filter"/> otherwise, and always when there are no
        /// options at all — which is what every caller that passes none already relies on.
        /// </summary>
        private static bool Accepts(FullTextSearchOptions? options, string path, PropertyInfo prop)
        {
            if (options == null)
                return true;

            if (options.PathFilter != null)
                return options.PathFilter.Invoke(path, prop);

            return options.Filter == null || options.Filter.Invoke(prop);
        }
    }
}
