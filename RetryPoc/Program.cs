using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RetryPoc.Application.Contracts;
using RetryPoc.Application.Models;
using RetryPoc.Application.Services;
using RetryPoc.Infrastructure;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

#region Serilog
builder.Services.AddLogging(loggingBuilder =>
      loggingBuilder.AddSerilog(dispose: true));
#endregion

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//services
builder.Services.AddDbContext<ICapDbContext, CapDbContext>();

builder.Services.AddTransient<IEventsRepository<PendingEventObject>, PendingEventsRepository>();
builder.Services.AddTransient<IFailSafeService, FailSafeService>();

builder.Services.AddCap(x =>
{
    x.FailedRetryInterval = builder.Configuration.GetValue<int>("Cap:RetryIntervalSec");
    x.FailedRetryCount = builder.Configuration.GetValue<int>("Cap:RetryCount");

    x.UsePostgreSql(o => { o.ConnectionString = builder.Configuration.GetValue<string>("Cap:PostgreSqlConnectionString"); o.Schema = Assembly.GetEntryAssembly()?.GetName().Name?.ToLower(); });
    x.UseAzureServiceBus(opt =>
    {
        opt.ConnectionString = builder.Configuration.GetValue<string>("Cap:AzureServiceBusConnectionString");
        opt.TopicPath = builder.Configuration.GetValue<string>("Cap:NewServiceRequestsTopicName");
    });

    x.FailedThresholdCallback = failed =>
    {
        // This is one place similar as SubscribeFilter we can inject notification to notify the system admin message sending failed
        // Either email notification or Log Error into App Insight to trigger the alert there
        Log.Error($@"A message failed after executing {x.FailedRetryCount} several times, 
                                 requiring manual troubleshooting. 
                                 Message name: {failed.Message.Headers["cap-msg-name"]} 
                                 value: {failed.Message.Value} cap-msg-id : {failed.Message.Headers["cap-msg-id"]}
        ");
    };
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
