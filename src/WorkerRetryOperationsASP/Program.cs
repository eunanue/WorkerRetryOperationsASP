using WorkerRetryOperationsASP;
using WorkerRetryOperationsASP.Configuration;
using WorkerRetryOperationsASP.Correlation;
using WorkerRetryOperationsASP.Data;
using WorkerRetryOperationsASP.Io;
using WorkerRetryOperationsASP.Orchestration;
using WorkerRetryOperationsASP.Parsing;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<LogSourceOptions>(builder.Configuration.GetSection("LogSource"));
builder.Services.Configure<RetryWorkerOptions>(builder.Configuration.GetSection("RetryWorker"));

builder.Services.AddSingleton<ILogFileLocator, DailyLogFileLocator>();
builder.Services.AddSingleton<ILogParserService, NotifyLogParserService>();
builder.Services.AddSingleton<ICorrelationService, CorrelationService>();
builder.Services.AddScoped<IWebhookRetryRepository, WebhookRetryRepository>();
builder.Services.AddScoped<IWebhookItravelStpRepository, WebhookItravelStpRepository>();
builder.Services.AddScoped<IStpWebhookInserter, StpWebhookInserter>();
builder.Services.AddScoped<IRetryOrchestrator, RetryOrchestrator>();
builder.Services.AddScoped<IDryRunReporter, DryRunReporter>();

builder.Services.AddHostedService<RetryWorker>();
builder.Services.AddWindowsService(o => o.ServiceName = "WorkerRetryOperationsASP");

var host = builder.Build();
await host.RunAsync();
