using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using ParcelPlan.Common;

namespace ParcelPlan.PredictEngine.Service.Entities
{
    public class LocaleDataEntity : IEntity
    {
        [BsonId]
        public ObjectId ObjectId { get; set; }
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Country { get; set; }
        public string PostalCode { get; set; }
        public string PostalCodePrefix { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string StateCode { get; set; }
    }
}
