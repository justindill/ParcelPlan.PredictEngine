using Microsoft.AspNetCore.Mvc;
using ParcelPlan.Common.MongoDB;
using ParcelPlan.Common;
using ParcelPlan.Common.Settings;
using ParcelPlan.PredictEngine.Service.Controllers;

var builder = WebApplication.CreateBuilder(args);
var serviceSettings = new ServiceSettings();
var Configuration = builder.Configuration;

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

serviceSettings = Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();

builder.Services.AddMongo()
                .AddMongoRepository<Log>("log");
                //.AddMassTransitWithRabbitMq();

builder.Services.AddHttpClient<PredictController>();

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
