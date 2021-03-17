using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelperDynamicClassMaps
{
    class Program
    {
        private static readonly List<UserDefinedFieldForCsvMapper> _fieldsForMapper = new()
        {
            new UserDefinedFieldForCsvMapper()
            {
                ColumnIndex = 1,
                FieldAlias = $"transactionid",
                FieldName = $"TransactionId"
            },
            new UserDefinedFieldForCsvMapper()
            {
                ColumnIndex = 2,
                FieldAlias = $"product_name",
                FieldName = $"Product.Name"
            },
            new UserDefinedFieldForCsvMapper()
            {
                ColumnIndex = 3,
                FieldAlias = $"product_sku",
                FieldName = $"Product.Sku"
            },
            new UserDefinedFieldForCsvMapper()
            {
                ColumnIndex = 4,
                FieldAlias = $"product_description",
                FieldName = $"Product.Description"
            },
            new UserDefinedFieldForCsvMapper()
            {
                ColumnIndex = 5,
                FieldAlias = $"promotion_date",
                FieldName = $"Promotion.Date"
            },
            new UserDefinedFieldForCsvMapper()
            {
                ColumnIndex = 6,
                FieldAlias = $"promotion_price",
                FieldName = $"Promotion.Price"
            }
        };

        private static LambdaExpression GenerateExpression<TModel>(string propertyName)
        {
            var parts = propertyName.Split('.');
            var rootPropertyName = parts[0];

            // Get a reference to the property
            var propertyInfo = ExpressionHelper.GetPropertyInfo<TModel>(rootPropertyName);  //TModel = Transaction Product.Name
            var model = ExpressionHelper.Parameter<TModel>();

            // Build the LINQ expression tree backwards:
            // x.Prop
            var key = ExpressionHelper.GetPropertyExpression(model, propertyInfo);
            // x => x.Prop
            var keySelector = ExpressionHelper.GetLambda(typeof(TModel), propertyInfo.PropertyType, model, key);

            return keySelector;
        }

        private static LambdaExpression GenerateExpression2<TModel>(string propertyName)
        {
            var model = ExpressionHelper.Parameter<TModel>();
            var memberExpression = model.GetMemberExpression(propertyName);
            var propertyInfo = memberExpression.Member as PropertyInfo;
            var propAsPropertyType = Expression.Convert(memberExpression, propertyInfo.PropertyType);

            // Need Expression<Func<TModel, TMember>>
            var keySelector = ExpressionHelper.GetLambda(
                typeof(TModel),
                propertyInfo.PropertyType,
                model,
                memberExpression);

            var generic = typeof(Func<, >);
            Type[] typeArgs = { typeof(TModel), propertyInfo.PropertyType };
            var constructed = generic.MakeGenericType(typeArgs);

            //var test = Expression.Lambda(constructed, memberExpression, Array.Empty<ParameterExpression>());

            //var test = Expression.Lambda<Func<TModel, object>>(propAsPropertyType, model);

            return keySelector;
        }

        private static (Type type, PropertyInfo propertyInfo) GetProp(Type baseType, string propertyName)
        {
            var parts = propertyName.Split('.');

            return (parts.Length > 1)
                ? GetProp(baseType.GetProperty(parts[0]).PropertyType, parts.Skip(1).Aggregate((a, i) => $"{a}.{i}"))
                : (baseType, baseType.GetProperty(propertyName));
        }

        static void Main(string[] args)
        {
            var dataFromExample1 = RunExampleWithStaticMapping();
            var dataFromExample2 = RunExampleWithStaticMappingUsingReferences();
            var dataFromExample3 = RunExampleWithDynamicMapping();
            Console.ReadLine();
        }

        /// <summary>
        /// Goal is to not have any ClassMaps defined at compile time. Build all class maps at run time from collection of UserDefinedFieldForCsvMapper.
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<Transaction> RunExampleWithDynamicMapping()
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            SetUpStreamWriterAndMemoryStreamWithTestData(stream, writer);

            var dynamicMap = BuildDynamicCsvClassMap<Transaction>(_fieldsForMapper);
            csv.Context.RegisterClassMap(dynamicMap);

            var records = csv.GetRecords<Transaction>().ToList();
            return records;
        }

        private static IEnumerable<Transaction> RunExampleWithStaticMapping()
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            SetUpStreamWriterAndMemoryStreamWithTestData(stream, writer);

            csv.Context.RegisterClassMap<TransactionMap>();

            var records = csv.GetRecords<Transaction>().ToList();
            return records;
        }

        private static IEnumerable<Transaction> RunExampleWithStaticMappingUsingReferences()
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            SetUpStreamWriterAndMemoryStreamWithTestData(stream, writer);

            csv.Context.RegisterClassMap<TransactionMapWithReferences>();

            var records = csv.GetRecords<Transaction>().ToList();
            return records;
        }

        private static void SetUpStreamWriterAndMemoryStreamWithTestData(MemoryStream stream, StreamWriter writer)
        {
            writer.WriteLine(
                "transactionid,product_name,product_sku,product_description,promotion_date,promotion_price");
            writer.WriteLine("12,xxx,1234,great product,12/4/202,$3.41");
            writer.Flush();
            stream.Position = 0;
        }

        /// <summary>
        /// Goal is to build a default class map from a collection of UserDefinedFieldForCsvMapper which contains strings for the property names. 
        /// For nested properties, each type name is separated by a period. How to build map at run time from strings?
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="userDefinedFieldsForCsvMapper"></param>
        /// <returns></returns>
        public static DefaultClassMap<TModel> BuildDynamicCsvClassMap<TModel>(
            IEnumerable<UserDefinedFieldForCsvMapper> userDefinedFieldsForCsvMapper)
            where TModel : class, new()
        {
            var defaultClassMap = new DefaultClassMap<TModel>();
            var type = typeof(TModel);

            foreach (var item in userDefinedFieldsForCsvMapper)
            {
                var member = type.GetProperty(item.FieldName);

                if (member is not null)
                {
                    defaultClassMap.Map(type, member)
                        .Name(GetUserDefinedFieldName(item.FieldName))
                        .Ignore(ShouldIgnore(item.FieldName))
                        .Index(GetIndex(item.FieldName));
                }
                else
                {
                    var recursivePropertryRetrieval = GetProp(type, item.FieldName); // Not sure this is useful yet.
                    var test = GenerateExpression<TModel>(item.FieldName);
                    var test2 = GenerateExpression2<TModel>(item.FieldName);

                    var expressionOfFuncReturnsString = test2 as Expression<Func<TModel, string>>;
                    var expressionOfFuncReturnsDateTime = test2 as Expression<Func<TModel, DateTime>>;

                    //defaultClassMap.Map(test);

                    //defaultClassMap.ReferenceMaps.Add(); Should we create member reference maps?

                    // Need to map member to Csv Field for nested properties.

                    if (expressionOfFuncReturnsString is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsString) // Given TModel generic type param, how to build Expression<Func<TModel, TMember>> at run time from information on hand?
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsDateTime is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsDateTime) // Given TModel generic type param, how to build Expression<Func<TModel, TMember>> at run time from information on hand?
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }

                    // Should we create an Expression<Func<TClass, TMember>>? How to infer both TClass and TMember?
                    // Should we build a class map for each type and use Reference maps? How?
                    // Should we split UserDefinedFieldForCsvMapper.FieldName into parts with var parts = propertyName.Split('.'); and process from here? How?
                }
            }

            int GetIndex(string fieldName) => userDefinedFieldsForCsvMapper
                    .FirstOrDefault(u => u.FieldName.IsEqualTo(fieldName))?.ColumnIndex ??
                0;

            string GetUserDefinedFieldName(string fieldName) => userDefinedFieldsForCsvMapper
                .FirstOrDefault(u => u.FieldName.IsEqualTo(fieldName))?.FieldAlias;

            bool ShouldIgnore(string fieldName) => userDefinedFieldsForCsvMapper
                    .Where(u => u.FieldName.IsEqualTo(fieldName))?.Count() !=
                1;

            return defaultClassMap;
        }

        public static PropertyInfo GetPropertyInfo<TSource, TProperty>(
            TSource source,
            Expression<Func<TSource, TProperty>> propertyLambda)
        {
            var type = typeof(TSource);

            if (propertyLambda.Body is not MemberExpression member)
            {
                throw new ArgumentException(
                    $"Expression '{propertyLambda.ToString()}' refers to a method, not a property.");
            }

            var propInfo = member.Member as PropertyInfo;
            if (propInfo is null)
            {
                throw new ArgumentException(
                    $"Expression '{propertyLambda.ToString()}' refers to a field, not a property.");
            }

            if (type != propInfo.ReflectedType && !type.IsSubclassOf(propInfo.ReflectedType))
            {
                throw new ArgumentException(
                    $"Expression '{propertyLambda.ToString()}' refers to a property that is not from type {type}.");
            }

            return propInfo;
        }

        //private static Expression<Func<TClass, TMember>> GenerateExpression<TClass, TMember>(Type containerType, string propertyName, string nestedPropertyName) {
        //    var parameterExpression = Expression.Parameter(containerType, "container");
        //    var property = Expression.Property(parameterExpression, propertyName);
        //    var nestedProperty = Expression.Property(property, nestedPropertyName);
        //    var lambda = Expression.Lambda<Func<TClass, TMember>>(nestedProperty, parameterExpression);
        //    return lambda;
        //}
    }

    // Sample from https://github.com/JoshClose/CsvHelper/issues/441
    /// <summary>
    /// Preferred way to set up a class map. I believe this sets up the legacy way with References for you. See below TransactionMapWithReferences.
    /// </summary>
    public class TransactionMap : ClassMap<Transaction>
    {
        public TransactionMap()
        {
            Map(m => m.TransactionId).Name("transactionid");
            Map(m => m.Product.Name).Name("product_name");
            Map(m => m.Product.Sku).Name("product_sku");
            Map(m => m.Product.Description).Name("product_description");
            Map(m => m.Promotion.Date).Name("promotion_date");
            Map(m => m.Promotion.Price).Name("promotion_price");
        }
    }

    /// <summary>
    /// This is a legacy way of setting up the ClassMap. 
    /// </summary>
    public class TransactionMapWithReferences : ClassMap<Transaction>
    {
        public TransactionMapWithReferences()
        {
            Map(m => m.TransactionId).Name("transactionid");
            References<ProductMap>(m => m.Product);
            References<PromotionMap>(m => m.Promotion);
        }
    }
}
