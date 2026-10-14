using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NODUS.API.Services;
using NODUS.Data;

var builder = WebApplication.CreateBuilder(args);

// Railway asigna el puerto dinámicamente vía PORT. Docker Compose y "dotnet run" (launchSettings)
// definen ASPNETCORE_URLS, que se respeta. Sin ninguna de las dos, se usa 5085.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls("http://0.0.0.0:5085");

// Railway provee DATABASE_URL (postgres://user:pass@host:port/db); si existe, tiene prioridad
// sobre appsettings.json. ConnectionStrings__NodusDB y Jwt__Key se toman solos de las variables de entorno.
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    builder.Configuration["ConnectionStrings:NodusDB"] = ConvertirDatabaseUrl(databaseUrl);
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(type =>
    {
        var assembly = type.Assembly.GetName().Name?.Replace(".", "_") ?? "Unknown";
        return $"{assembly}_{type.Name}";
    });
});

builder.Services.AddDbContext<NodusDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("NodusDB")));

// QuestPDF: licencia Community (gratuita para proyectos académicos y organizaciones chicas)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
builder.Services.AddSingleton<AlmacenamientoService>();
builder.Services.AddScoped<RemitoService>();
builder.Services.AddScoped<EstadosOTService>();
builder.Services.AddScoped<ReportesFallaService>();

// Nominatim exige un User-Agent que identifique a la aplicación
builder.Services.AddHttpClient<GeocodingService>(client =>
{
    client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("NODUS-GestionPrestamos/1.0");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Toda la API exige un usuario interno salvo los endpoints con [AllowAnonymous] o con su propio [Authorize].
    // Así Personal asistencial no llega a ningún módulo aunque llame a la API a mano.
    var usuarioInterno = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx => !ctx.User.IsInRole(Roles.PersonalAsistencial))
        .Build();
    options.AddPolicy(Politicas.UsuarioInterno, usuarioInterno);
    options.FallbackPolicy = usuarioInterno;

    // Ficha QR y reporte de fallas: cualquier usuario autenticado, incluido Personal asistencial
    options.AddPolicy(Politicas.CualquierUsuario, p => p.RequireAuthenticatedUser());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (ctx, _) =>
        new ValueTask(ctx.HttpContext.Response.WriteAsync("Enviaste demasiados reportes. Esperá unos minutos y volvé a intentar."));

    // 10 reportes de falla cada 10 minutos por usuario (el id sale del token)
    options.AddPolicy(LimitesTasa.ReportesFalla, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                origin == "http://localhost:3000" ||
                origin == "http://localhost:5173" ||
                origin == "http://localhost:5174" ||
                origin.EndsWith(".devtunnels.ms") ||
                origin.EndsWith(".vercel.app"))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Se aplican las migraciones al iniciar (en Docker y Railway la base arranca vacía).
// Con reintentos porque el contenedor de PostgreSQL puede tardar en aceptar conexiones.
const int intentosMigracion = 10;
for (var intento = 1; ; intento++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NodusDbContext>();
        db.Database.Migrate();
        break;
    }
    catch (Exception ex) when (intento < intentosMigracion)
    {
        app.Logger.LogWarning("No se pudo migrar la base (intento {Intento}/{Total}): {Mensaje}. Reintentando en 5 s...",
            intento, intentosMigracion, ex.Message);
        Thread.Sleep(TimeSpan.FromSeconds(5));
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Los PDFs y escaneos de remitos se abren con un link directo (<a href>), que no lleva el token:
// quedan accesibles sin sesión, como estaban antes de exigir autenticación en la API.
string[] documentosSinToken =
{
    "api/Prestamos/{id}/remito", "api/Prestamos/{id}/remito-devolucion",
    "api/Remitos/{id}/firma-salida", "api/Remitos/{id}/firma-devolucion"
};
app.MapControllers().Add(endpoint =>
{
    if (endpoint is RouteEndpointBuilder ruta
        && documentosSinToken.Contains(ruta.RoutePattern.RawText, StringComparer.OrdinalIgnoreCase))
        endpoint.Metadata.Add(new AllowAnonymousAttribute());
});
app.Run();

static string ConvertirDatabaseUrl(string databaseUrl)
{
    var uri = new Uri(databaseUrl);
    var credenciales = uri.UserInfo.Split(':', 2);

    var csb = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(credenciales[0]),
        Password = credenciales.Length > 1 ? Uri.UnescapeDataString(credenciales[1]) : null,
        // La red privada de Railway no usa SSL y el proxy público sí: Prefer cubre ambos casos
        SslMode = Npgsql.SslMode.Prefer
    };
    return csb.ConnectionString;
}