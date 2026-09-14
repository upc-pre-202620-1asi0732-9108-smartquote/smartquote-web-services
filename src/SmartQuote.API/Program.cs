using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SmartQuote.API.Configuration;
using SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.API.Security;
using SmartQuote.API.Shared.Application.Security;
using SmartQuote.API.Shared.Domain;
using SmartQuote.API.Shared.Infrastructure;
using SmartQuote.API.SupplyRequests;
using SmartQuote.API.SupplyRequests.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.EvaluationSimulation;
using SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Modules.PurchaseOrdering;
using SmartQuote.Modules.QuotationIntake;
using SmartQuote.Modules.QuotationIntake.Infrastructure.Persistence.EFC.Configuration;
using SmartQuote.Shared.Interfaces.Middleware;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is required. Configure it with dotnet user-secrets or ConnectionStrings__DefaultConnection.");
}

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Issuer) ||
    string.IsNullOrWhiteSpace(jwt.Audience) ||
    Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Issuer, Jwt:Audience and a Jwt:SigningKey of at least 32 bytes are required.");
}

var aiProvider = builder.Configuration["AI:Provider"] ?? "Stub";
if (aiProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) &&
    string.IsNullOrWhiteSpace(builder.Configuration["OpenAI:ApiKey"]))
{
    throw new InvalidOperationException(
        "OpenAI:ApiKey is required when AI:Provider is OpenAI. Store it in user-secrets locally or OpenAI__ApiKey in the deployment environment.");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IDomainEventDispatcher, InProcessDomainEventDispatcher>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("SmartQuoteClients", policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SmartQuote RESTful API",
        Version = "v1",
        Description = "Backend for poultry supply requests, quotation intake, evaluation simulations and purchase ordering."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT access token issued for a SmartQuote development or production user."
    });
});

builder.Services.AddSupplyRequestsModule(builder.Configuration, connectionString);
builder.Services.AddQuotationIntakeModule(builder.Configuration, connectionString);
builder.Services.AddEvaluationSimulationModule(builder.Configuration, connectionString);
builder.Services.AddPurchaseOrderingModule(builder.Configuration, connectionString);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<SupplyRequestsDbContext>("supply-requests-database")
    .AddDbContextCheck<QuotationIntakeDbContext>("quotation-intake-database")
    .AddDbContextCheck<EvaluationSimulationDbContext>("evaluation-simulation-database")
    .AddDbContextCheck<PurchaseOrderingDbContext>("purchase-ordering-database");

var app = builder.Build();

if (builder.Configuration.GetValue("Database:ApplyMigrations", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<SupplyRequestsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<QuotationIntakeDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<EvaluationSimulationDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<PurchaseOrderingDbContext>().Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("SmartQuoteClients");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();

app.Run();

public partial class Program;
