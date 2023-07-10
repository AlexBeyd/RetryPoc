using Microsoft.Extensions.DependencyInjection;
using RetryPoc.Infrastructure;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLogging();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<CapDbContext>();

builder.Services.AddCap(x =>
{
    x.FailedRetryInterval = 10;
    x.UsePostgreSql(builder.Configuration.GetValue<string>("Cap:PostgreSqlConnectionString"));
    x.UseAzureServiceBus(opt =>
    {
        opt.ConnectionString = builder.Configuration.GetValue<string>("Cap:AzureServiceBusConnectionString");
        opt.TopicPath = builder.Configuration.GetValue<string>("Cap:NewServiceRequestsTopicName");
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
