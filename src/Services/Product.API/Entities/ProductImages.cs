using Contracts.Domains.Interfaces;

namespace Product.API.Entities
{
    public class ProductImages : EntityAuditBase<long>
    {
        public long ProductId
        {
            get; set;
        }

        public string AssetKey
        {
            get; set;
        }
        public string AltText
        {
            get; set;
        }
        public string Position
        {
            get; set;
        }
    }
}
