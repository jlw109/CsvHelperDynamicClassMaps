using System.Linq.Expressions;

namespace CsvHelperDynamicClassMaps
{
    public static class ParameterExpressionExtensions
    {
        // To handle inner classes properties, we need a recursive method.
        private static MemberExpression GetMemberExpression(Expression param, string propertyName)
        {
            if (!propertyName.Contains("."))
            {
                return Expression.PropertyOrField(param, propertyName);
            }

            var index = propertyName.IndexOf(".");
            var subParam = Expression.PropertyOrField(param, propertyName.Substring(0, index));
            return GetMemberExpression(subParam, propertyName.Substring(index + 1));
        }

        /// <summary>
        /// Gets a member expression for a specific property.
        /// </summary>
        /// <param name="param"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static MemberExpression GetMemberExpression(this ParameterExpression param, string propertyName) { return GetMemberExpression(
                                                                                                                      (Expression)param,
                                                                                                                      propertyName); }
    }
}
