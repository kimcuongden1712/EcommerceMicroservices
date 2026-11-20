using Contracts.Domains.Interfaces;

namespace Product.API.Entities
{
    public class CatalogOutbox : EntityAuditBase<long>
    {
        public string Topic
        {
            get; set;
        }

        public string PayloadJson
        {
            get; set;
        }
        public string Attempts
        {
            get; set;
        }
        public DateTimeOffset? NextAttemptAt
        {
            get; set;
        }
        public DateTimeOffset? PublishedAt
        {
            get; set;
        }

        public DateTimeOffset? OccurredAt
        {
            get; set;
        }
    }
}
