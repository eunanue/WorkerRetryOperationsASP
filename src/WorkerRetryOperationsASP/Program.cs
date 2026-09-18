using Serilog;
using Serilog.Events;
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

var fileLogOptions = builder.Configuration.GetSection("FileLog").Get<FileLogOptions>() ?? new FileLogOptions();
var fileLogFolder = Path.IsPathRooted(fileLogOptions.FolderPath)
    ? fileLogOptions.FolderPath
    : Path.Combine(AppContext.BaseDirectory, fileLogOptions.FolderPath);

// Log propio del worker, un archivo por día ({prefijo}{yyyyMMdd}.log) con retención.
// writeToProviders mantiene consola y Event Log de Windows además del archivo.
builder.Services.AddSerilog(logger => logger
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.File(
        Path.Combine(fileLogFolder, $"{fileLogOptions.FileNamePrefix}.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: fileLogOptions.RetainedFileCountLimit,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
        encoding: System.Text.Encoding.UTF8),
    writeToProviders: true);

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
