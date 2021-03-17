using Nito.Comparers;

namespace CsvHelperDynamicClassMaps
{
    public class Transaction : ComparableBase<Transaction>
    {
        static Transaction() => DefaultComparer =
            ComparerBuilder.For<Transaction>()
                .OrderBy(p => p.TransactionId)
                .ThenBy(p => p.Product.Name)
                .ThenBy(p => p.Product.Sku)
                .ThenBy(p => p.Product.Description)
                .ThenBy(p => p.Promotion.Date)
                .ThenBy(p => p.Promotion.Price);

        public Product Product { get; set; }

        public Promotion Promotion { get; set; }

        public int TransactionId { get; set; }
    }
}
