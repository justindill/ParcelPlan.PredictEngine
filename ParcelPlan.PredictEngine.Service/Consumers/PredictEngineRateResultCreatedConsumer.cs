using MassTransit;
using ParcelPlan.Common;
using ParcelPlan.Common.MassTransit.Contracts;
using ParcelPlan.PredictEngine.Service.Entities;

namespace ParcelPlan.PredictEngine.Service.Consumers
{
    public class PredictEngineRateResultCreatedConsumer : IConsumer<PredictEngineRateResultCreated>
    {
        private readonly IRepository<Log> logRepository;

        public PredictEngineRateResultCreatedConsumer(IRepository<Log> logRepository)
        {
            this.logRepository = logRepository;
        }

        public async Task Consume(ConsumeContext<PredictEngineRateResultCreated> context)
        {
            await context.RespondAsync(context.Message);
        }

        private async Task LogMessageAsync(Level level, string message)
        {
            var _log = new Log
            {
                Controller = "ModelEngine.Service.Consumers.RateResultCreatedConsumer",
                Level = level.ToString(),
                Message = message
            };

            await logRepository.CreateAsync(_log);
        }
    }
}
