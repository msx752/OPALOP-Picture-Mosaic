using Opalop.Infrastructure;
using Opalop.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<TileProcessorService>();
builder.Services.AddHostedService<StaleMessageClaimerService>();

var host = builder.Build();
host.Run();
