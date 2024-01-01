using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using ParcelPlan.Common;

namespace ParcelPlan.PredictEngine.Service.Entities
{
    public class AS_LocaleDataEntity : IEntity
    {
        [BsonId]
        public ObjectId ObjectId { get; set; }
        public Guid Id { get; set; } = Guid.NewGuid();
        public string PostalCodePrefix { get; set; }
        public string PostalCode { get; set; }
        public string ChargeCode { get; set; }
    }
}
