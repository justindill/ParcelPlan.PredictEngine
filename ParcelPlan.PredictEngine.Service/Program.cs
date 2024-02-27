using Microsoft.AspNetCore.Mvc;
using ParcelPlan.Common.MongoDB;
using ParcelPlan.Common;
using ParcelPlan.Common.Settings;
using ParcelPlan.PredictEngine.Service.Controllers;
using MassTransit;
using System.Reflection;
using ParcelPlan.PredictEngine.Service.Entities;
using ParcelPlan.PredictEngine.Service.Consumers;

var builder = WebApplication.CreateBuilder(args);
var serviceSettings = new ServiceSettings();
var Configuration = builder.Configuration;

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

serviceSettings = Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();

builder.Services.AddMongo()
                .AddMongoRepository<LocaleDataEntity>("locale")
                .AddMongoRepository<AS_LocaleDataEntity>("as_locale")
                .AddMongoRepository<SpecialLocaleEntity>("special_locale")
                .AddMongoRepository<Log>("log");
//.AddMassTransitWithRabbitMq();

builder.Services.AddMassTransit(configure =>
{
    var entryAssembly = Assembly.GetExecutingAssembly();

    configure.AddConsumers(entryAssembly);

    configure.UsingRabbitMq((context, configurator) =>
    {
        var configuration = context.GetService<IConfiguration>();
        var serviceSettings = configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
        var rabbitMQSettings = configuration.GetSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>();

        configurator.Host(rabbitMQSettings.Host);

        configurator.ReceiveEndpoint("predict-service-request-created", e =>
        {
            e.Durable = false;
            e.ConfigureConsumer<PredictServiceRequestCreatedConsumer>(context);
            e.PrefetchCount = 32;
        });

        configurator.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(serviceSettings.ServiceName, false));

        configurator.UseMessageRetry(retryConfigurator =>
        {
            retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
        });
    });
});

builder.Services.AddMvc().AddControllersAsServices();

builder.Services.AddHttpClient<PredictServiceController>();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = actionContext =>
    {
        return new BadRequestObjectResult(new
        {
            Code = 400,
            Request_Id = Guid.NewGuid(),
            Messages = actionContext.ModelState.Values.SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
        });
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
