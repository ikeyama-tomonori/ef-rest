using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EfRest.Internal
{
    internal static class TypeExtention
    {
        public static PropertyInfo? GetPropertyInfo(
            this Type type,
            string name,
            JsonSerializerOptions jsonSerializerOptions)
        {
            var stringComparison = jsonSerializerOptions.PropertyNameCaseInsensitive
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
            var properties = type
                .GetProperties()
                .FirstOrDefault(propertyInfo =>
                {
                    if (propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>() != null) return false;
                    if (propertyInfo.GetCustomAttribute<NotMappedAttribute>() != null) return false;
                    var jsonPropertyNameAttribute = propertyInfo
                        .GetCustomAttribute<JsonPropertyNameAttribute>();

                    if (jsonPropertyNameAttribute != null)
                    {
                        return jsonPropertyNameAttribute.Name.Equals(name, stringComparison);
                    }
                    var convertedName = jsonSerializerOptions
                        .PropertyNamingPolicy?
                        .ConvertName(propertyInfo.Name)
                        ?? propertyInfo.Name;
                    return convertedName.Equals(name, stringComparison);
                });
            return properties;
        }

        public static Expression? GetMemberExpression(
            this Type type,
            string name,
            Expression parameter,
            JsonSerializerOptions jsonSerializerOptions)
        {
            (Expression expression, Type type)? seed = (expression: parameter, type);
            var member = name
                .Split('.')
                .Aggregate(
                    seed,
                    (accumulator, current) =>
                    {
                        if (accumulator is (Expression param, Type type))
                        {
                            var propertyInfo = type.GetPropertyInfo(current, jsonSerializerOptions);
                            if (propertyInfo == null) return null;
                            var memberAccess = Expression.MakeMemberAccess(param, propertyInfo);
                            return (memberAccess, propertyInfo.PropertyType);
                        }
                        return null;
                    })?
                .expression;
            return member;
        }
    }
}
