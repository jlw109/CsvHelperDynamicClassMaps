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

        private static LambdaExpression GenerateExpressionForMappingMemberOfTModelToCsvField<TModel>(
            string propertyName)
        {
            var model = ExpressionHelper.Parameter<TModel>();
            var memberExpression = model.GetMemberExpression(propertyName);
            var propertyInfo = memberExpression.Member as PropertyInfo;
            //var propAsPropertyType = Expression.Convert(memberExpression, propertyInfo.PropertyType);

            // Need Expression<Func<TModel, TMember>>
            var keySelector = ExpressionHelper.GetLambda(
                typeof(TModel),
                propertyInfo.PropertyType,
                model,
                memberExpression);

            //var generic = typeof(Func<, >);
            //Type[] typeArgs = { typeof(TModel), propertyInfo.PropertyType };
            //var constructed = generic.MakeGenericType(typeArgs);

            return keySelector;
        }

        static void Main(string[] args)
        {
            var dataFromExample1 = RunExampleWithStaticMapping();
            var dataFromExample2 = RunExampleWithStaticMappingUsingReferences();
            var dataFromExample3 = RunExampleWithDynamicMapping();
            var doesExample1EqualExample2 = dataFromExample1.FirstOrDefault().Equals(dataFromExample2.FirstOrDefault());
            var doesExample1EqualExample3 = dataFromExample1.FirstOrDefault().Equals(dataFromExample3.FirstOrDefault());

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
                    // Need to map member to Csv Field for nested properties.
                    var generatedLamdaExpressionForMapppingMemberToCsvField = GenerateExpressionForMappingMemberOfTModelToCsvField<TModel>(
                        item.FieldName);

                    //Handle most system types.
                    var expressionOfFuncReturnsString = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, string>>;
                    var expressionOfFuncReturnsDateTime = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, DateTime>>;
                    var expressionOfFuncReturnsNullableDateTime = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, DateTime?>>;
                    var expressionOfFuncReturnsDecimal = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, decimal>>;
                    var expressionOfFuncReturnsNullableDecimal = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, decimal?>>;
                    var expressionOfFuncReturnsDouble = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, double>>;
                    var expressionOfFuncReturnsNullableDouble = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, double?>>;
                    var expressionOfFuncReturnsInt = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, int>>;
                    var expressionOfFuncReturnsNullableInt = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, int?>>;
                    var expressionOfFuncReturnsGuid = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, Guid>>;
                    var expressionOfFuncReturnsNullableGuid = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, Guid?>>;
                    var expressionOfFuncReturnsBool = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, bool>>;
                    var expressionOfFuncReturnsNullableBool = generatedLamdaExpressionForMapppingMemberToCsvField as Expression<Func<TModel, bool?>>;

                    if (expressionOfFuncReturnsString is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsString) // Given TModel generic type param, how to build Expression<Func<TModel, TMember>> at run time from information on hand, given TMember is of type string?
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsDateTime is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsDateTime)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsNullableDateTime is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsNullableDateTime)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsDecimal is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsDecimal)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsNullableDecimal is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsNullableDecimal)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsDouble is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsDouble)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsNullableDouble is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsNullableDouble)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsInt is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsInt)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsNullableInt is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsNullableInt)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsGuid is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsGuid)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsNullableGuid is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsNullableGuid)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsBool is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsBool)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
                    if (expressionOfFuncReturnsNullableBool is not null)
                    {
                        defaultClassMap.Map(expressionOfFuncReturnsNullableBool)
                            .Name(GetUserDefinedFieldName(item.FieldName))
                            .Ignore(ShouldIgnore(item.FieldName))
                            .Index(GetIndex(item.FieldName));
                    }
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
