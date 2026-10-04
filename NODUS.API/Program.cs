using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NODUS.API.Services;
using NODUS.Data;

var builder = WebApplication.CreateBuilder(args);

// Railway asigna el puerto dinámicamente vía PORT; localmente se usa 5085
var port = Environment.GetEnvironmentVariable("PORT");
builder.WebHost.UseUrls(string.IsNullOrEmpty(port)
    ? "http://localhost:5085"
    : $"http://0.0.0.0:{port}");

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
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