using System.Linq.Expressions;

namespace Sync.Infrastructure.Grid;

/// <summary>
/// Builds dynamic LINQ Where expressions from jqGrid filter structures.
/// Supports: string (cn, eq, ne, bw, ew), int/int? (eq, ne, lt, le, gt, ge),
/// decimal/decimal? (eq, ne, lt, le, gt, ge), Guid/Guid? (eq, ne),
/// DateTime/DateTime? (eq, ne, lt, le, gt, ge).
/// </summary>
public static class JqGridHelper
{
    /// <summary>
    /// Applies jqGrid filter rules to an IQueryable, building a combined Where clause.
    /// </summary>
    public static IQueryable<T> ApplyFilters<T>(IQueryable<T> query, JqGridFilter filter)
    {
        if (filter.Rules.Count == 0)
            return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combined = null;

        foreach (var rule in filter.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Field) || string.IsNullOrWhiteSpace(rule.Data))
                continue;

            // Normalize field name: capitalize first letter to match C# property names
            var fieldName = char.ToUpperInvariant(rule.Field[0]) + rule.Field[1..];

            // Validate property exists on type T (prevent arbitrary property access)
            var propInfo = typeof(T).GetProperty(fieldName);
            if (propInfo == null)
                continue;

            var property = Expression.Property(parameter, fieldName);
            var propertyType = propInfo.PropertyType;
            var expr = BuildFilterExpression(property, propertyType, rule);
            if (expr == null)
                continue;

            combined = combined == null
                ? expr
                : filter.GroupOp.Equals("OR", StringComparison.OrdinalIgnoreCase)
                    ? Expression.OrElse(combined, expr)
                    : Expression.AndAlso(combined, expr);
        }

        if (combined != null)
        {
            var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);
            query = query.Where(lambda);
        }

        return query;
    }

    /// <summary>
    /// Applies dynamic sorting to an IQueryable using expression trees.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(IQueryable<T> query, string? sortBy, string? sortOrder)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return query;

        var fieldName = char.ToUpperInvariant(sortBy[0]) + sortBy[1..];
        var propInfo = typeof(T).GetProperty(fieldName);
        if (propInfo == null)
            return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, fieldName);
        var lambda = Expression.Lambda(property, parameter);

        var methodName = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase)
            ? "OrderByDescending"
            : "OrderBy";

        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            [typeof(T), property.Type],
            query.Expression,
            Expression.Quote(lambda));

        return query.Provider.CreateQuery<T>(resultExpression);
    }

    private static Expression? BuildFilterExpression(
        MemberExpression property, Type propertyType, JqGridFilterRule rule)
    {
        var op = rule.Op.ToLowerInvariant();

        // --- string ---
        if (propertyType == typeof(string))
        {
            var constant = Expression.Constant(rule.Data);
            return op switch
            {
                "eq" => Expression.Equal(property, constant),
                "ne" => Expression.NotEqual(property, constant),
                "cn" => Expression.Call(property,
                    typeof(string).GetMethod("Contains", [typeof(string)])!, constant),
                "bw" => Expression.Call(property,
                    typeof(string).GetMethod("StartsWith", [typeof(string)])!, constant),
                "ew" => Expression.Call(property,
                    typeof(string).GetMethod("EndsWith", [typeof(string)])!, constant),
                _ => null
            };
        }

        // --- Guid / Guid? ---
        if (propertyType == typeof(Guid) || propertyType == typeof(Guid?))
        {
            if (!Guid.TryParse(rule.Data, out var guidValue))
                return null;
            var isNullable = propertyType == typeof(Guid?);
            Expression prop = isNullable ? Expression.Property(property, "Value") : property;
            var constant = Expression.Constant(guidValue, typeof(Guid));
            return op switch
            {
                "eq" => Expression.Equal(prop, constant),
                "ne" => Expression.NotEqual(prop, constant),
                _ => null
            };
        }

        // --- decimal / decimal? ---
        if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
        {
            if (!decimal.TryParse(rule.Data, System.Globalization.CultureInfo.InvariantCulture, out var decValue))
                return null;
            var isNullable = propertyType == typeof(decimal?);
            Expression prop = isNullable ? Expression.Property(property, "Value") : property;
            var constant = Expression.Constant(decValue, typeof(decimal));
            return BuildComparisonExpression(prop, constant, op);
        }

        // --- DateTime / DateTime? ---
        if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
        {
            if (!DateTime.TryParse(rule.Data, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dtValue))
                return null;
            var isNullable = propertyType == typeof(DateTime?);
            Expression prop = isNullable ? Expression.Property(property, "Value") : property;
            var constant = Expression.Constant(dtValue, typeof(DateTime));
            return BuildComparisonExpression(prop, constant, op);
        }

        // --- int / int? ---
        if (propertyType == typeof(int) || propertyType == typeof(int?))
        {
            if (!int.TryParse(rule.Data, out var intValue))
                return null;
            var isNullable = propertyType == typeof(int?);
            Expression prop = isNullable ? Expression.Property(property, "Value") : property;
            var constant = Expression.Constant(intValue, typeof(int));
            return BuildComparisonExpression(prop, constant, op);
        }

        return null;
    }

    private static Expression? BuildComparisonExpression(
        Expression property, Expression constant, string op)
    {
        return op switch
        {
            "eq" => Expression.Equal(property, constant),
            "ne" => Expression.NotEqual(property, constant),
            "lt" => Expression.LessThan(property, constant),
            "le" => Expression.LessThanOrEqual(property, constant),
            "gt" => Expression.GreaterThan(property, constant),
            "ge" => Expression.GreaterThanOrEqual(property, constant),
            _ => null
        };
    }
}
