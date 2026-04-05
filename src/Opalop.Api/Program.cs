using Opalop.Api.Components;
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
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.MapInboundClaims = false; // Keep original claim names (sub, email, etc.)

        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // Extract Keycloak realm roles from nested realm_access.roles claim
                if (context.Principal?.Identity is System.Security.Claims.ClaimsIdentity identity)
                {
                    var realmAccess = context.Principal.FindFirst("realm_access");
                    if (realmAccess is not null)
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(realmAccess.Value);
                        if (doc.RootElement.TryGetProperty("roles", out var roles))
                        {
                            foreach (var role in roles.EnumerateArray())
                                identity.AddClaim(new System.Security.Claims.Claim("role", role.GetString()!));
                        }
                    }
                }
                return Task.CompletedTask;
            }
        };

        // Accept tokens issued from both localhost (external) and container hostname (internal)
        var validIssuers = builder.Configuration.GetSection("Keycloak:ValidIssuers").Get<string[]>();
        if (validIssuers is { Length: > 0 })
        {
            options.TokenValidationParameters.ValidIssuers = validIssuers;
            options.TokenValidationParameters.ValidateIssuer = true;
        }
    });
builder.Services.AddAuthorization();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminPolicy", policy => policy.RequireClaim("role", "admin"));

builder.Services.AddScoped<MosaicOrchestrator>();
builder.Services.AddHttpClient("GooglePhotos");
builder.Services.AddScoped<GooglePhotosImporter>();
builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRazorComponents();

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
app.MapAdminEndpoints();
app.MapImportEndpoints();
app.MapHub<MosaicProgressHub>("/hubs/mosaic");

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapRazorComponents<App>();

app.Run();
