using Contracts.Domains.Interfaces;

namespace Product.API.Entities
{
    public class ProductImageUploads : EntityAuditBase<long>
    {
        public long ProductId
        {
            get; set;
        }

        public string StagingKey
        {
            get; set;
        }
        public string? AssetKey
        {
            get; set;
        }
        public string ExpectedContentType
        {
            get; set;
        }
        public int Status
        {
            get; set;
        }
        public DateTimeOffset? ExpriesAt
        {
            get; set;
        }
    }
}
