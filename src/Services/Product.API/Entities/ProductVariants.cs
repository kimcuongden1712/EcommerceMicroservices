using Contracts.Domains.Interfaces;

namespace Product.API.Entities
{
    public class ProductVariants : EntityAuditBase<long>
    {
        public long ProductId
        {
            get; set;
        }

        public string Sku
        {
            get; set;
        }
        public string ColorCode
        {
            get; set;
        }
        public string ColorLabel
        {
            get; set;
        }
        public string SizeCode
        {
            get; set;
        }
        public string SizeLabel
        {
            get; set;
        }
        public decimal PriceMinor
        {
            get; set;
        }
        public int status
        {
            get; set;
        }

        public int WeightG
        {
            get; set;
        }
        public int LengthMm
        {
            get; set;
        }
        public int WidthMm
        {
            get; set;
        }
        public int HeightMm
        {
            get; set;
        }
        public DateTimeOffset? PublishedAt
        {
            get; set;
        }
        public string Version
        {
            get; set;
        }
    }
}
