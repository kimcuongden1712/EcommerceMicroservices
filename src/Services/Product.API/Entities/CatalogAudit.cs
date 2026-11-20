using Contracts.Domains.Interfaces;

namespace Product.API.Entities
{
    public class CatalogAudit : EntityAuditBase<long>
    {
        public int EntityType
        {
            get; set;
        }

        public long EntityId
        {
            get; set;
        }
        public string Action
        {
            get; set;
        }
        public long ActorId
        {
            get; set;
        }
        public long CorrelationId
        {
            get; set;
        }
        public string ChangedFieldJson
        {
            get; set;
        }

        public DateTimeOffset? OccurredAt
        {
            get; set;
        }
    }
}
