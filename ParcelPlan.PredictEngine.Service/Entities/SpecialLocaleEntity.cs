using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using ParcelPlan.Common;

namespace ParcelPlan.PredictEngine.Service.Entities
{
    public class SpecialLocaleEntity : IEntity
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public Guid Id { get; set; }
        public SpecialPostalCode SpecialPostalCode { get; set; } = new();
    }

    public class SpecialPostalCode
    {
        public string AS_ChargeCode { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string PostalCodePrefix
        {
            get
            {
                return this.PostalCode.Substring(0, 3);
            }
        }
        public string WinningService { get; set; }
        public string PredictedService { get; set; }
    }
}
