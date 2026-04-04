using Opalop.Api.Endpoints;
using Opalop.Api.Hubs;
using Opalop.Api.Services;
using Opalop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.Audience = builder.Configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = false;
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<MosaicOrchestrator>();
builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapPhotoEndpoints();
app.MapResourceEndpoints();
app.MapMosaicEndpoints();
app.MapAccountEndpoints();
app.MapHub<MosaicProgressHub>("/hubs/mosaic");

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();
