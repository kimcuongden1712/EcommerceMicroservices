using Contracts.Domains.Interfaces;

namespace Product.API.Entities
{
    public class Products : EntityAuditBase<long>
    {
        public long CategoryId
        {
            get; set;
        }

        public string Name
        {
            get; set;
        }
        public string Slug
        {
            get; set;
        }
        public string Description
        {
            get; set;
        }
        public int status
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
