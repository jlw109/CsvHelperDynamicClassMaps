using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

                    // Need to map member to Csv Field for nested properties.

                    //defaultClassMap.Map(m => m.TransactionId) // Given TModel generic type param, how to build Expression<Func<TModel, TMember>> at run time from information on hand?
                    //    .Name(GetUserDefinedFieldName(item.FieldName))
                    //    .Ignore(ShouldIgnore(item.FieldName))
                    //    .Index(GetIndex(item.FieldName));

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

        //private static Expression<Func<TClass, TMember>> GenerateExpression<TClass, TMember>(Type containerType, string propertyName, string nestedPropertyName) {
        //    var parameterExpression = Expression.Parameter(containerType, "container");
        //    var property = Expression.Property(parameterExpression, propertyName);
        //    var nestedProperty = Expression.Property(property, nestedPropertyName);
        //    var lambda = Expression.Lambda<Func<TClass, TMember>>(nestedProperty, parameterExpression);
        //    return lambda;
        //}
    }

    // Sample from https://github.com/JoshClose/CsvHelper/issues/441
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
