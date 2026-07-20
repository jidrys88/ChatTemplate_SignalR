using System.Text;
using ChatTemplate.API.Filters;
using ChatTemplate.API.Hubs;
using ChatTemplate.API.Security;
using ChatTemplate.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1) Konfiguration einlesen
// ---------------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var jwtSecretKey = jwtSection["SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey ist nicht konfiguriert.");
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

var corsOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? [];

// ---------------------------------------------------------------------------
// 2) Infrastructure-Schicht registrieren (DB-Umschaltung, PresenceTracker, ...)
// ---------------------------------------------------------------------------
builder.Services.AddChatInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// 3) SignalR registrieren inkl. globalem Fehler-Filter und optionalem Scale-Out (Redis/Azure)
// ---------------------------------------------------------------------------
builder.Services
    .AddSignalR(options =>
    {
        options.AddFilter<SignalRErrorHandlingFilter>();
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    })
    .AddJsonProtocol(options =>
    {
        // System.Text.Json explizit konfigurieren (z.B. camelCase, wie vom JS-Client erwartet).
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    })
    .AddChatScaleOut(builder.Configuration); // Redis-Backplane / Azure SignalR - siehe appsettings.json

// Custom IUserIdProvider: notwendig, damit Clients.User(userId) fuer Direktnachrichten funktioniert.
builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();

// ---------------------------------------------------------------------------
// 4) JWT-Bearer-Authentifizierung inkl. WebSocket-Unterstuetzung
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // WICHTIG: Browser-basierte SignalR-Clients koennen bei WebSocket-Verbindungen
        // keine Authorization-Header setzen. Daher wird das JWT alternativ aus dem
        // Query-String extrahiert - ausschliesslich fuer Requests an den Hub-Endpunkt.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ---------------------------------------------------------------------------
// 5) CORS - notwendig, da SignalR-Browser-Clients i.d.R. von einem anderen Origin kommen
// ---------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("ChatClientPolicy", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Erforderlich fuer SignalR mit Cookies/Credentials
    });
});

// ---------------------------------------------------------------------------
// 6) Standard-Web-API-Dienste
// ---------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------------------------------------------------------------------------
// 7) Middleware-Pipeline
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles(); // liefert wwwroot/index.html als Standarddatei aus (Web-Test-Client)
app.UseStaticFiles();

app.UseCors("ChatClientPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Der zentrale, stark typisierte Chat-Hub. Der Pfad "/hubs/chat" wird auch in
// Program.cs (JwtBearerEvents.OnMessageReceived) sowie in den Client-Beispielen referenziert.
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
