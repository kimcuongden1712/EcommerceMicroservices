using Contracts.Domains.Interfaces;

namespace Product.API.Entities
{
    public class Categories : EntityAuditBase<long>
    {
        public long? ParentId
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

        public int Status
        {
            get; set;
        }

        public int SortOrder
        {
            get; set;
        }

        public string Version
        {
            get; set;
        }
    }
}
