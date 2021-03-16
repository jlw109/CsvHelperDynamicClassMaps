using CsvHelper.Configuration;

namespace CsvHelperDynamicClassMaps
{
    public class ProductMap : ClassMap<Product>
    {
        public ProductMap()
        {
            Map(m => m.Name).Name("product_name");
            Map(m => m.Sku).Name("product_sku");
            Map(m => m.Description).Name("product_description");
        }
    }
}
