using CsvHelper.Configuration;

namespace CsvHelperDynamicClassMaps
{
    public class PromotionMap : ClassMap<Promotion>
    {
        public PromotionMap()
        {
            Map(m => m.Date).Name("promotion_date");
            Map(m => m.Price).Name("promotion_price");
        }
    }
}
