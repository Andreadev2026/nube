# Arquitectura de software del sistema NODUS

> Sistema de Control Patrimonial Hospitalario: gestión de activos biomédicos, mantenimiento, insumos, préstamos interhospitalarios y reportes.
>
> Este documento describe la arquitectura **tal como está implementada en el código fuente** de la solución `NODUS.WPF.slnx` y del proyecto `nodus-frontend`. Cada afirmación remite a archivos reales del repositorio.

---

## 1. Arquitectura general

### 1.1 Estilo arquitectónico

NODUS combina dos estilos complementarios:

1. **Cliente-servidor desacoplado (SPA + API REST).** El cliente es una *Single Page Application* en React que corre en el navegador y el servidor es una API HTTP en ASP.NET Core. Se comunican solamente mediante peticiones HTTP con cuerpos JSON. Ninguno de los dos comparte código ni proceso con el otro; el único contrato entre ambos son las rutas `/api/...` y la forma de los objetos JSON.

2. **Arquitectura en capas en el backend.** Del lado del servidor, la solución .NET separa las responsabilidades en proyectos (ensamblados) distintos, con dependencias que van en un único sentido:

```
NODUS.API  ──►  NODUS.Data  ──►  NODUS.Models
    │                               ▲
    └───────────────────────────────┘
```

- **Capa de presentación / servicios HTTP**: `NODUS.API` (controllers, DTOs, servicios de aplicación).
- **Capa de acceso a datos**: `NODUS.Data` (`NodusDbContext`, migraciones).
- **Capa de dominio / entidades**: `NODUS.Models` (clases POCO sin dependencias).

`NODUS.Models` no referencia ningún otro proyecto (ver `NODUS.Models/NODUS.Models.csproj`), así que el modelo de dominio no depende de la tecnología de persistencia ni de la web. `NODUS.Data` depende solo de `NODUS.Models` y de EF Core, y `NODUS.API` depende de ambos.

Esto es una **arquitectura en capas relajada**: los controllers usan directamente el `DbContext` (no hay una capa de repositorios ni de servicios de negocio intermedia para todas las operaciones). Es una decisión habitual en APIs de tamaño medio con EF Core, porque el propio `DbContext` ya implementa los patrones *Unit of Work* (`SaveChangesAsync`) y *Repository* (cada `DbSet<T>`). La lógica que no pertenece al acceso a datos (generación de PDFs, almacenamiento de archivos, geocodificación) sí se extrajo a servicios de aplicación (ver §2.7).

### 1.2 Organización de la solución

La solución `NODUS.WPF.slnx` contiene cinco proyectos .NET. El frontend es un proyecto Node independiente, fuera de la solución.

| Proyecto | Tipo | Target | Responsabilidad |
|---|---|---|---|
| **NODUS.Models** | Biblioteca de clases | `net10.0` | Entidades del dominio como clases POCO: `Equipo`, `Prestamo`, `OrdenTrabajo`, `Intervencion`, `Usuario`, `HospitalExterno`, `Insumo`, `Alerta`, etc. (21 entidades). Declaran propiedades escalares, claves foráneas (`IdEquipo`, `IdHospital`) y propiedades de navegación (`Equipo? Equipo`, `ICollection<ProrrogaPrestamo> Prorrogas`). No tienen atributos de mapeo ni lógica de persistencia. |
| **NODUS.Data** | Biblioteca de clases | `net10.0` | Acceso a datos con EF Core. Contiene `NodusDbContext` (mapeo objeto-relacional con Fluent API y datos semilla), `NodusDbContextFactory` (fábrica para las herramientas de diseño) y la carpeta `Migrations/` con el historial versionado del esquema. |
| **NODUS.API** | ASP.NET Core Web API | `net10.0` | Punto de entrada del backend. Contiene `Program.cs` (composición e inyección de dependencias), 19 controllers en `Controllers/`, los contratos de entrada y salida en `DTOs/` y los servicios de aplicación en `Services/` (`RemitoService`, `AlmacenamientoService`, `GeocodingService`, `ZonaHoraria`). También guarda los PDFs generados y los escaneos firmados en `Storage/`. |
| **NODUS.Services** | Biblioteca de clases | `net10.0` | Reservado para una futura capa de lógica de negocio independiente de la web. **Hoy no tiene código fuente**: solo el `.csproj`, que referencia a `NODUS.Data` y `NODUS.Models`. Los servicios que existen están en `NODUS.API/Services`. |
| **NODUS.WPF** | Aplicación de escritorio WPF | `net10.0-windows` | Cliente de escritorio que se exploró en una etapa temprana (el repositorio conserva ese nombre). Usa el patrón **MVVM** con `CommunityToolkit.Mvvm` (`ViewModels/MarcaViewModel.cs` hereda de `ObservableObject` y usa `[ObservableProperty]`). A diferencia del frontend web, **accede a la base directamente**: en `App.xaml.cs` crea una instancia estática de `NodusDbContext` con la cadena de conexión de su propio `appsettings.json`. Solo tiene implementado el ABM de marcas; es un prototipo, no forma parte de la arquitectura productiva. |
| **nodus-frontend** | SPA React + Vite | Node | Interfaz web productiva: login, menú lateral por rol, 21 pantallas en `src/pages/`, mapas, gráficos y exportación a PDF/Excel. Consume exclusivamente la API REST. |

> **Nota para la tesis:** la coexistencia de `NODUS.WPF` (cliente pesado con acceso directo a la base, arquitectura de dos niveles) y `nodus-frontend` (cliente liviano que pasa por la API, arquitectura de tres niveles) puede presentarse como la evolución arquitectónica del proyecto. Pasar a la API centraliza las reglas de negocio y la seguridad en el servidor, evita distribuir credenciales de la base en cada puesto y permite usar el sistema desde cualquier navegador.

---

## 2. Backend

### 2.1 Framework y versión

- **ASP.NET Core Web API sobre .NET 10** (`<TargetFramework>net10.0</TargetFramework>` y `Sdk="Microsoft.NET.Sdk.Web"` en `NODUS.API/NODUS.API.csproj`).
- Modelo de *hosting mínimo*: `Program.cs` usa `WebApplication.CreateBuilder(args)` sin clase `Startup`.
- Paquetes principales:

| Paquete | Versión | Uso |
|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.x | Proveedor EF Core para PostgreSQL |
| `Microsoft.EntityFrameworkCore.Tools` | 10.0.0 | Comandos de migraciones (`NODUS.Data`) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.8 | Validación de tokens JWT |
| `BCrypt.Net-Next` | 4.2.0 | Hash de contraseñas |
| `QuestPDF` | 2026.9.0 | Generación de remitos en PDF |
| `Swashbuckle.AspNetCore` | 10.1.7 | Documentación OpenAPI/Swagger |

El servidor escucha en `http://localhost:5085` (`Properties/launchSettings.json`) y en desarrollo abre Swagger UI automáticamente (`launchUrl: "swagger"`).

### 2.2 Pipeline e inyección de dependencias (`Program.cs`)

`Program.cs` es la **raíz de composición**: ahí se registran todos los servicios en el contenedor de inyección de dependencias nativo de ASP.NET Core y se arma el *pipeline* de middlewares.

| Registro | Ciclo de vida | Motivo |
|---|---|---|
| `AddDbContext<NodusDbContext>(UseNpgsql(...))` | *Scoped* (por petición) | Cada petición HTTP tiene su propio contexto y su propia unidad de trabajo |
| `AddScoped<RemitoService>()` | *Scoped* | Trabaja con el `DbContext` de la petición |
| `AddSingleton<AlmacenamientoService>()` | *Singleton* | No tiene estado mutable; solo resuelve rutas dentro de `Storage/` |
| `AddHttpClient<GeocodingService>(...)` | *Typed client* | `HttpClient` administrado por `IHttpClientFactory`, preconfigurado para Nominatim (OpenStreetMap) |

El orden de los middlewares es:

```csharp
app.UseCors("ReactApp");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

Además, la serialización JSON usa `ReferenceHandler.IgnoreCycles` para que no falle la serialización de entidades con navegaciones bidireccionales (por ejemplo `Equipo.Prestamos` ↔ `Prestamo.Equipo`) cuando un controller devuelve entidades en lugar de DTOs.

### 2.3 Acceso a datos: EF Core, Code-First y PostgreSQL

**Enfoque Code-First.** El esquema no se diseña en el motor: se deriva de las clases de `NODUS.Models` y de la configuración de `NODUS.Data/NodusDbContext.cs`. EF Core compara ese modelo con la última instantánea (`Migrations/NodusDbContextModelSnapshot.cs`) y genera las migraciones que llevan la base al nuevo estado.

**Motor.** PostgreSQL, conectado con el proveedor Npgsql. La cadena de conexión `NodusDB` se lee de `appsettings.json` (`Host=localhost;Port=5432;Database=nodus_db;...`).

**Mapeo con Fluent API.** `NodusDbContext` expone un `DbSet<T>` por cada entidad (21 en total) y configura el mapeo completo en `OnModelCreating`, sin *data annotations* en las entidades. Así el dominio queda libre de detalles de persistencia. La configuración resuelve las diferencias de convención entre C# (PascalCase) y PostgreSQL (snake_case):

```csharp
// NODUS.Data/NodusDbContext.cs — entidad Equipo
modelBuilder.Entity<Equipo>(e =>
{
    e.ToTable("equipo"); e.HasKey(x => x.IdEquipo);
    e.Property(x => x.IdEquipo).HasColumnName("id_equipo");
    e.Property(x => x.Modelo).HasColumnName("modelo").HasMaxLength(100).IsRequired();
    e.Property(x => x.NroSerie).HasColumnName("nro_serie").HasMaxLength(100).IsRequired();
    ...
    e.HasIndex(x => x.NroSerie).IsUnique();
    e.HasOne(x => x.Marca).WithMany(m => m.Equipos)
        .HasForeignKey(x => x.IdMarca).OnDelete(DeleteBehavior.Restrict);
});
```

Qué se configura con la Fluent API:

| Aspecto | Ejemplo en el proyecto |
|---|---|
| Nombre de tabla | `ToTable("orden_trabajo")` |
| Clave primaria simple | `HasKey(x => x.IdOT)` |
| Clave primaria compuesta | `RemitoContador`: `HasKey(x => new { x.Tipo, x.Anio })` |
| Nombre de columna | `HasColumnName("fecha_estimada_devolucion")` |
| Longitud y obligatoriedad | `HasMaxLength(20).IsRequired()` → `character varying(20) NOT NULL` |
| Índices únicos | `Equipo.NroSerie`, `Prestamo.NumeroRemito`, `Prestamo.NumeroRemitoDevolucion` |
| Relaciones 1:N | `HasOne(...).WithMany(...).HasForeignKey(...)` |
| Varias FK a la misma tabla | `Ubicacion` → `SectorAnterior` y `SectorNuevo`, ambas contra `sector` |
| Comportamiento al borrar | `Restrict` por defecto (protege la trazabilidad histórica); `Cascade` solo en entidades dependientes: `EquipoComponente`, `PrestamoComponente`, `ProrrogaPrestamo` |

La política de borrado es una decisión de diseño deliberada: en un sistema patrimonial no se puede borrar un equipo que tenga órdenes de trabajo, préstamos o bajas asociadas, porque se perdería su historia. Los controllers además validan esto antes de borrar para devolver un mensaje claro (por ejemplo `MarcasController.DeleteMarca` rechaza el borrado si la marca tiene equipos).

**Datos semilla (*seed data*).** Al final de `OnModelCreating`, `HasData(...)` carga los catálogos iniciales: los cinco estados de equipo con su color de semáforo (`En servicio`/verde, `En mantenimiento`/amarillo, `Fuera de servicio`/rojo, `En préstamo`/azul, `Baja patrimonial`/gris), marcas, tipos de activo, servicios, sectores e insumos. Como forman parte del modelo, se versionan dentro de las migraciones.

**Consultas.** Se escriben con LINQ asíncrono (`ToListAsync`, `FirstOrDefaultAsync`). Hay tres técnicas para cargar datos relacionados:

- *Eager loading* con `Include`/`ThenInclude` (`PrestamosController.CargarParaRemitoAsync`).
- `AsSplitQuery()` en `GetPrestamos`, para no generar un producto cartesiano al incluir varias colecciones.
- **Proyección directa a DTO** en `GetMapa`: `.Select(p => new PrestamoMapaDto { ... })` se traduce a un único `SELECT` con los *joins* necesarios y trae solo las columnas que usa el mapa.

**Transacciones y SQL nativo.** Cuando una operación tiene que ser atómica se usa una transacción explícita. El caso más representativo es la numeración correlativa de remitos (`REM-2026-0001`, `DEV-2026-0001`) en `RemitoService.ReservarNumeroAsync`, que usa un *UPSERT* nativo de PostgreSQL:

```csharp
INSERT INTO remito_contador (tipo, anio, ultimo_numero) VALUES ({tipo}, {anio}, 1)
ON CONFLICT (tipo, anio) DO UPDATE SET ultimo_numero = remito_contador.ultimo_numero + 1
RETURNING ultimo_numero
```

Se ejecuta con `Database.SqlQuery<int>($"...")`, que convierte la cadena interpolada en una consulta parametrizada (protegida contra inyección SQL). Se llama dentro de la transacción del alta del préstamo (`BeginTransactionAsync` … `CommitAsync`): el *UPSERT* bloquea la fila hasta el commit y, si la transacción se revierte, el contador también. Así no quedan números repetidos ni salteados aunque haya peticiones concurrentes.

### 2.4 Migraciones con EF Core CLI

`NODUS.Data` referencia `Microsoft.EntityFrameworkCore.Tools`, y `NodusDbContextFactory` implementa `IDesignTimeDbContextFactory<NodusDbContext>`. Esa fábrica permite que las herramientas de línea de comandos creen el contexto **sin arrancar la API**, porque le pasa directamente la cadena de conexión a Npgsql.

Flujo de trabajo:

```bash
# 1. Se modifica una entidad en NODUS.Models y/o su mapeo en NodusDbContext
# 2. Se genera la migración (código C# con Up/Down + actualización del snapshot)
dotnet ef migrations add AddProrrogasPrestamo --project NODUS.Data

# 3. Se aplica sobre la base PostgreSQL
dotnet ef database update --project NODUS.Data

# (opcional) Script SQL idempotente para desplegar en otro entorno
dotnet ef migrations script --idempotent --project NODUS.Data
```

Cada migración genera tres cosas:

- `<timestamp>_<Nombre>.cs`, con `Up()` (aplica el cambio) y `Down()` (lo revierte). Por ejemplo, `20260909030342_AddPrestamos.cs` crea las tablas `hospital_externo` y `prestamo` con columnas `timestamp with time zone`, `character varying(n)` e identidad `IdentityByDefaultColumn`.
- `<timestamp>_<Nombre>.Designer.cs`, con los metadatos del modelo en ese punto.
- La actualización de `NodusDbContextModelSnapshot.cs`.

El repositorio tiene **13 migraciones**, del 13/05/2026 al 23/09/2026, que documentan cómo evolucionó el esquema:

| Migración | Cambio |
|---|---|
| `InitialCreate` / `CreacionInicial` | Esquema base (marcas, equipos, OT, intervenciones, insumos…) |
| `AgregarUsuario` | Tabla de usuarios con hash de contraseña |
| `AgregarSectorYRefactorUbicacion` | Sectores y traslados con sector anterior/nuevo |
| `AddEquipoComponente` | Checklist de componentes por equipo |
| `AddPrestamos`, `AddMotivoPrestamo` | Módulo de préstamos interhospitalarios |
| `AddCoordenadasHospitalExterno` | Latitud y longitud para el mapa |
| `AddRemitoPrestamo`, `AddFechaEmisionRemito` | Remitos PDF numerados |
| `AddProrrogasPrestamo` | Historial de prórrogas |
| `AddConfirmacionEntregaDevolucion`, `SimplificarFirmasRemitos` | Remitos de devolución y escaneos firmados |

La API **no aplica migraciones al arrancar** (no hay `Database.Migrate()` en `Program.cs`). Se aplican de forma explícita con la CLI, así que actualizar el esquema es un paso controlado y separado del despliegue del código.

### 2.5 Patrón DTO

**Qué problema resuelve.** Las entidades de `NODUS.Models` representan cómo se guardan los datos; la API necesita exponer lo que el cliente necesita ver o enviar. Si se usan las entidades como contrato público aparecen varios problemas:

- **Exposición de datos internos**: rutas de archivos en el servidor, hashes de contraseña.
- **Sobre-posteo (*over-posting*)**: el cliente podría asignar campos que no debería controlar, como el estado o el número de remito.
- **Ciclos de serialización** por las navegaciones bidireccionales.
- **Acoplamiento**: cualquier cambio en el esquema rompe el contrato con el frontend.

**Ejemplo real: préstamos.** La entidad `NODUS.Models/Prestamo.cs` tiene, entre otras, estas propiedades internas:

```csharp
public string? RutaRemitoPdf { get; set; }             // "Remitos/REM-2026-0001.pdf"
public string? RutaRemitoSalidaFirmado { get; set; }
public string? RutaRemitoDevolucionFirmado { get; set; }
public ICollection<PrestamoComponente> PrestamoComponentes { get; set; }
```

El contrato de salida `NODUS.API/DTOs/PrestamoDto.cs` **no expone las rutas del sistema de archivos**. Las reemplaza por indicadores booleanos y aplana las relaciones:

```csharp
public bool TieneRemitoPdf { get; set; }
public bool TieneFirmaSalida { get; set; }
public EquipoResumenDto? Equipo { get; set; }      // solo IdEquipo, Modelo, NroSerie
public UsuarioResumenDto? Usuario { get; set; }    // solo IdUsuario, Nombre
public List<string> Componentes { get; set; }      // nombres, no entidades
public List<ProrrogaDto> Prorrogas { get; set; }   // con "AutorizadoPor" resuelto
```

La conversión la hace el método estático `ToDto(Prestamo p)` en `PrestamosController`. Es un *mapper* manual, sin AutoMapper:

```csharp
TieneRemitoPdf = !string.IsNullOrEmpty(p.RutaRemitoPdf),
Componentes = p.PrestamoComponentes.Select(c => c.Componente).ToList(),
```

Para la entrada, `PrestamoInputDto` (cuerpo de `POST /api/prestamos`) solo acepta lo que el usuario puede decidir: equipo, hospital, fechas, motivo, observaciones y componentes. El `Estado` (`"En préstamo"`), el `NumeroRemito` (reservado por `RemitoService`) y las rutas de los PDFs **los asigna el servidor**, así que el cliente no puede manipularlos.

Hay además DTOs específicos por caso de uso: `PrestamoMapaDto` (datos para ubicar préstamos activos en el mapa), `ProrrogaInputDto` y `RemitoDevolucionInputDto`. El módulo de equipos sigue el mismo esquema con `EquipoDto`/`EquipoInputDto`. El comentario de `EquipoInputDto.cs` explica el motivo: *"Reemplaza al binding directo sobre la entidad Equipo, para poder aceptar también la lista de componentes marcados en el formulario"*.

**Variantes que conviven en el proyecto.** El patrón no se aplicó de manera uniforme; hay tres niveles:

1. **DTOs formales en `DTOs/`**: equipos, préstamos, hospitales externos (los módulos más complejos y más recientes).
2. **Clases de request anidadas o proyecciones anónimas**: `UsuariosController` recibe `UsuarioCreateRequest`/`UsuarioUpdateRequest` y responde con `Select(u => new { u.IdUsuario, u.Nombre, u.Rol, u.Email })`, lo que garantiza que **`PasswordHash` nunca sale de la API**. `AuthController` usa `LoginRequest`.
3. **Entidad como contrato**: los catálogos simples (`MarcasController`, `TiposActivoController`, `SectoresController`…) reciben y devuelven la entidad directamente. En entidades planas sin datos sensibles es aceptable, pero es la razón por la que hace falta `ReferenceHandler.IgnoreCycles`.

### 2.6 Controllers y diseño REST

Los 19 controllers siguen la misma estructura:

```csharp
[Route("api/[controller]")]   // → /api/prestamos, /api/equipos, ...
[ApiController]               // validación automática del modelo, binding del body, respuestas 400 estándar
public class PrestamosController : ControllerBase
{
    private readonly NodusDbContext _db;           // inyectado por constructor
    private readonly RemitoService _remitos;
    ...
}
```

**Verbos HTTP y convenciones REST** (ejemplo `MarcasController`):

| Verbo | Ruta | Acción | Respuesta |
|---|---|---|---|
| `GET` | `/api/marcas` | Listar | `200 OK` + colección |
| `GET` | `/api/marcas/{id}` | Obtener uno | `200 OK` / `404 Not Found` |
| `POST` | `/api/marcas` | Crear | `201 Created` + `Location` (`CreatedAtAction`) |
| `PUT` | `/api/marcas/{id}` | Reemplazar | `204 No Content` / `400` / `404` |
| `DELETE` | `/api/marcas/{id}` | Borrar | `204 No Content` / `400` si tiene dependencias |

Todas las acciones son **asíncronas** (`async Task<ActionResult<T>>`), para no bloquear hilos del servidor mientras se espera a la base.

**Sub-recursos y acciones de negocio.** Cuando una operación no es un CRUD puro, se modela como sub-recurso o como acción sobre el recurso, siempre con `POST` porque cambia estado:

| Endpoint | Semántica |
|---|---|
| `POST /api/prestamos/{id}/devolucion` | Registra la devolución: cierra el préstamo, devuelve el equipo a *En servicio* y genera el remito DEV |
| `POST /api/prestamos/{id}/prorrogar` | Extiende la fecha estimada y guarda la prórroga en el historial |
| `GET /api/prestamos/{id}/remito?descargar=true` | Devuelve el PDF (en línea o como adjunto) |
| `POST /api/remitos/{id}/subir-firma-salida` | Carga el escaneo firmado (multipart) |
| `GET /api/prestamos/mapa` | Vista de lectura especializada para el mapa |
| `GET /api/ordenestrabajo/abiertas`, `GET /api/insumos/criticos` | Consultas filtradas |
| `POST /api/alertas/generar-automaticas`, `POST /api/hojasdevida/generar-para-todos` | Procesos en lote |
| `PUT /api/hospitalesexternos/{id}/coordenadas`, `POST /api/hospitalesexternos/geocodificar` | Actualización parcial y geocodificación |

**Reglas de negocio en el controller.** Por ejemplo, `PrestamosController.PostPrestamo` valida que el equipo exista y esté *En servicio* (`ID_ESTADO_EN_SERVICIO = 1`), que el hospital exista y que las fechas sean coherentes. Después, dentro de una transacción, crea el préstamo, reserva el número de remito y cambia el estado del equipo a *En préstamo* (`ID_ESTADO_EN_PRESTAMO = 4`). La generación del PDF queda **fuera** de la transacción y dentro de un `try/catch`: si falla, el préstamo queda registrado igual y el remito se regenera la próxima vez que se pida (`GetRemito`). Este patrón de **degradación controlada** evita que un fallo secundario anule una operación de negocio.

Los errores de validación se devuelven como `400 Bad Request` con un mensaje en español listo para mostrar al usuario (por ejemplo `"El equipo no está en servicio, no se puede prestar en su estado actual."`). El frontend lo muestra directamente con `err.response?.data`.

### 2.7 Servicios de aplicación

La lógica que no es acceso a datos ni orquestación HTTP vive en `NODUS.API/Services/`:

| Servicio | Responsabilidad | Detalle destacable |
|---|---|---|
| `RemitoService` | Numeración correlativa y generación de PDFs de remitos de salida (REM) y devolución (DEV) | Arma el documento con la API *fluent* de QuestPDF; licencia Community declarada en `Program.cs` |
| `AlmacenamientoService` | Guardar y leer archivos en `{ContentRoot}/Storage` | En la base se guarda solo la ruta relativa. `RutaAbsoluta` impide salir de `Storage/` (**protección contra *path traversal***). `ValidarEscaneoAsync` reconoce el tipo real del archivo por su firma binaria (*magic numbers* `%PDF`, `FF D8 FF`, `89 50 4E 47`), no por la extensión, y limita el tamaño a 10 MB |
| `GeocodingService` | Pasar la dirección de un hospital a coordenadas con Nominatim (OpenStreetMap) | Respeta la política de uso con un `SemaphoreSlim` y 1,1 s entre peticiones, y un `User-Agent` identificable |
| `ZonaHoraria` | Hora oficial de Argentina | Centraliza la conversión UTC ↔ hora local |

`ArchivoResultExtensions.Archivo(...)` es un método de extensión sobre `ControllerBase` que unifica cómo se sirven los archivos: `Content-Disposition: inline` para verlos en el navegador o como adjunto con `?descargar=true`.

### 2.8 Autenticación con JWT

**Almacenamiento de credenciales.** Las contraseñas nunca se guardan en texto plano. `UsuariosController.PostUsuario` y `PutUsuario` guardan `BCrypt.Net.BCrypt.HashPassword(password)` en la columna `password_hash`. BCrypt aplica *salt* aleatorio y un factor de costo adaptativo.

**Emisión del token** (`AuthController`):

1. `POST /api/auth/login` recibe `{ usuario, password }`. El campo `usuario` acepta nombre o email.
2. Se busca el usuario y se verifica la contraseña con `BCrypt.Verify`. Si falla, responde `401 Unauthorized` con el mismo mensaje genérico (`"Credenciales inválidas."`) haya fallado el usuario o la contraseña, para no revelar qué usuarios existen.
3. `GenerarToken` construye un `JwtSecurityToken`:
   - **Claims**: `NameIdentifier` (id), `Name`, `Role` y `Email`.
   - **Firma**: HMAC-SHA256 (`SecurityAlgorithms.HmacSha256`) con clave simétrica `Jwt:Key`.
   - **Emisor/audiencia**: `NODUS.API` / `NODUS.Frontend`.
   - **Expiración**: `Jwt:ExpireMinutes` = 480 minutos (una jornada laboral de 8 horas).
4. La respuesta es `{ token, usuario: { idUsuario, nombre, rol, email } }`.

**Validación del token** (`Program.cs`). Se registra el esquema `JwtBearer` con validación de emisor, audiencia, vigencia y firma (`ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`), y se agregan los middlewares `UseAuthentication()` y `UseAuthorization()`. Con esto, cualquier petición que traiga `Authorization: Bearer <token>` queda asociada a un `ClaimsPrincipal` con el rol del usuario.

**Roles.** El rol viaja como *claim* estándar `ClaimTypes.Role`. Eso permite usar en el servidor el mecanismo nativo `[Authorize(Roles = "...")]` de ASP.NET Core. Los roles del sistema son: *Administrador*, *Jefe patrimonio*, *Técnico biomédico*, *Jefe servicio*, *Médico*, *Residente*, *Enfermero* y *Auditor ANMAT*.

> ⚠️ **Estado actual de la implementación (importante para redactar la tesis con precisión).** La infraestructura JWT está completa en cuanto a **emisión** y **configuración de validación**, pero **todavía no se exige**:
> - Ningún controller ni acción tiene el atributo `[Authorize]`, y no hay una política de autorización global (`FallbackPolicy`). Por eso todos los endpoints de `/api/*` responden sin token.
> - El frontend recibe el token en el login pero **no lo envía** en las peticiones siguientes (ver §3.3).
> - El control de acceso por rol hoy se aplica **solo en la interfaz** (ver §4.2).
>
> En la sección 6 se detallan los cambios para completar el circuito. Se recomienda que la tesis lo describa como **diseño previsto** o como **trabajo futuro**, salvo que se implemente antes de la entrega.

---

## 3. Frontend

### 3.1 Framework y librerías

`nodus-frontend` es una SPA construida con:

| Librería | Versión | Uso en el proyecto |
|---|---|---|
| **React** | 19.2 | Biblioteca de UI basada en componentes funcionales y *hooks* |
| **Vite** | 8.0 | Servidor de desarrollo con HMR y *bundler* de producción (`vite.config.js` con `@vitejs/plugin-react`) |
| **React Router DOM** | 7.15 | Ruteo del lado del cliente (`BrowserRouter`, `Routes`, `Route`, `NavLink`, `Navigate`, `useLocation`) |
| **Axios** | — | Cliente HTTP (`src/api.js`) |
| **Leaflet / React-Leaflet** | 1.9 / 5.0 | Mapa de préstamos activos (`MapaPrestamos.jsx`) |
| **Recharts** | 3.10 | Gráficos del dashboard (`Reportes.jsx`) |
| **jsPDF + jspdf-autotable, html2canvas** | 4.2 / 5.0 / 1.4 | Exportación de reportes y fichas a PDF en el cliente |
| **xlsx (SheetJS)** | 0.18 | Exportación de listados a Excel |
| **react-datepicker, date-fns** | 9.1 / 4.4 | Selección y formato de fechas |
| **@tabler/icons-react, react-icons** | — | Iconografía |

El proyecto usa módulos ES (`"type": "module"`), JavaScript con JSX (sin TypeScript) y ESLint con las reglas de `react-hooks` y `react-refresh`.

La URL del backend se configura con la variable de entorno de Vite `VITE_API_URL` (`.env`: `http://localhost:5085`). Así se puede apuntar a otro servidor sin tocar el código.

> Nota técnica: `src/api.js` importa `axios` y `src/App.jsx` importa `@tabler/icons-react`, pero ninguna de las dos está declarada en `package.json` ni instalada en `node_modules`. Antes de la entrega conviene ejecutar `npm install axios @tabler/icons-react` para que el proyecto compile en una instalación limpia.

### 3.2 Estructura del código

```
nodus-frontend/src/
├── main.jsx                 # Punto de entrada: createRoot(...).render(<StrictMode><App/></StrictMode>)
├── App.jsx                  # Shell de la aplicación: estado de sesión, Sidebar, tabla de rutas, RutaProtegida
├── api.js                   # Instancia única de Axios configurada con la URL base de la API
├── permisos.js              # Matriz de permisos por rol (fuente única de verdad)
├── vencimientos.js          # Lógica de dominio compartida: vencimientos de préstamos y garantías
├── documentosPrestamo.js    # Utilidades para documentos de préstamo
├── components/
│   └── DocumentosPrestamo.jsx
├── data/
│   └── catalogoComponentesRespiratorios.js   # Catálogo estático para el checklist de componentes
└── pages/                   # Una página por caso de uso (21 pantallas)
    ├── Login.jsx, Bienvenida.jsx
    ├── Activos.jsx, Ubicaciones.jsx, BajasPatrimoniales.jsx, FichaEquipo.jsx     # Patrimonial
    ├── OrdenesTrabajo.jsx, Intervenciones.jsx, HojasDeVida.jsx, Alertas.jsx       # Mantenimiento
    ├── Insumos.jsx, UsosInsumo.jsx                                                # Insumos
    ├── Prestamos.jsx, HospitalesExternos.jsx, MapaPrestamos.jsx, Remitos.jsx      # Préstamos
    ├── Reportes.jsx                                                               # Reportes
    └── Usuarios.jsx, Marcas.jsx, Sectores.jsx, Servicios.jsx                      # Administración / maestros
```

La organización es **por funcionalidad (pantalla)**: cada archivo de `pages/` agrupa la vista, su estado y sus llamadas a la API. La lógica que se reutiliza entre pantallas se extrae a módulos planos (`permisos.js`, `vencimientos.js`).

### 3.3 Ruteo y protección de rutas por rol

**Dos niveles de control de acceso en `App.jsx`:**

**Nivel 1: autenticación.** El componente `App` guarda la sesión en `const [auth, setAuth] = useState(null)`. Si `auth` es `null`, no se monta el router: se muestra solo `<Login onLogin={handleLogin} />`. Ninguna ruta interna existe hasta que el usuario inicia sesión.

```jsx
if (!auth) {
  return <Login onLogin={handleLogin} />;
}
return (<BrowserRouter> ... </BrowserRouter>);
```

**Nivel 2: autorización por módulo.** El componente `RutaProtegida` envuelve cada pantalla restringida y consulta la matriz de `permisos.js`:

```jsx
function RutaProtegida({ auth, modulo, children }) {
  const rol = auth?.usuario?.rol;
  if (!puedeVerModulo(rol, modulo)) {
    return <Navigate to="/" replace />;
  }
  return children;
}

<Route path="/prestamos"
       element={<RutaProtegida auth={auth} modulo="prestamos"><Prestamos auth={auth} /></RutaProtegida>} />
```

Es un **componente de orden superior por composición** (patrón *guard*): si el rol no tiene acceso al módulo, redirige a la bienvenida con `replace`, así la URL prohibida no queda en el historial del navegador.

La misma función `puedeVerModulo` decide qué secciones muestra el menú lateral (`Sidebar`). De esta forma el menú y las rutas nunca se contradicen: un usuario no ve enlaces a pantallas que no puede abrir, y si escribe la URL a mano, `RutaProtegida` lo redirige.

Rutas sin protección de módulo: `/` (Bienvenida), `/ficha/:idEquipo` (ficha pública del equipo, pensada para acceder desde un código QR; ahí el permiso se controla por acción con `puedeHacer(rol, 'crear_ot')`), `/alertas` y los maestros `/marcas`, `/sectores` y `/servicios`. La ruta comodín `*` redirige a `/`.

### 3.4 Comunicación con el backend (`api.js`)

```js
// src/api.js
import axios from 'axios';

const api = axios.create({
  baseURL: `${import.meta.env.VITE_API_URL}/api`
});

export default api;
```

Es una **instancia única compartida** (patrón *singleton* de módulo ES). Todas las pantallas la importan y usan rutas relativas: `api.get('/prestamos')`, `api.post('/auth/login', {...})`, `api.put(\`/marcas/${id}\`, {...})`, `api.delete(...)`. Axios serializa los objetos a JSON, pone `Content-Type: application/json` y deserializa la respuesta en `res.data`.

Los errores se manejan en cada pantalla con `try/catch`, mostrando el mensaje que devuelve la API: `alert(err.response?.data || 'Error al eliminar.')`.

**Manejo del JWT: estado actual.** `Login.jsx` recibe `{ token, usuario }` y lo pasa a `App` con `onLogin(res.data)`, que lo guarda en el estado `auth`. Sin embargo:

- `api.js` **no tiene interceptor** que agregue `Authorization: Bearer <token>`. En `src/` no aparecen `Authorization`, `Bearer`, `interceptors` ni referencias a `auth.token`.
- El token **no se persiste** (solo se guarda en `localStorage` el nombre de usuario, si se marca "Recordar mi usuario"). Al recargar la página se pierde la sesión y hay que volver a iniciarla.
- La identidad del usuario se envía como dato del cuerpo en algunas operaciones (por ejemplo `idUsuario: auth?.usuario?.idUsuario` al crear préstamos y prórrogas), en lugar de que el servidor la obtenga del token.

En la sección 6 está la implementación sugerida.

### 3.5 Manejo del estado

La aplicación **no usa una librería de estado global** (ni Redux, ni Zustand, ni MobX, ni React Context). El estado se maneja con los mecanismos nativos de React:

| Mecanismo | Uso en NODUS |
|---|---|
| **`useState` local** | Cada página guarda su propio estado: la lista traída de la API y los campos del formulario. En `Marcas.jsx`: `const [marcas, setMarcas] = useState([])`, un `useState` por campo y `editando` para alternar entre alta y edición |
| **`useEffect`** | Carga inicial de datos al montar el componente: `useEffect(() => { cargarMarcas(); }, [])`. En el `Sidebar`, vuelve a consultar los contadores cuando cambia `location.pathname` |
| **Props (*lifting state up*)** | El estado de sesión vive en el componente raíz `App` y baja por props: `<Sidebar auth={auth} onLogout={handleLogout} />`, `<Prestamos auth={auth} />`. `Login` avisa hacia arriba con el *callback* `onLogin` |
| **Eventos del DOM (`CustomEvent`)** | Para sincronizar componentes hermanos sin estado global: cuando `Prestamos.jsx` modifica datos, emite `window.dispatchEvent(new CustomEvent('nodus:prestamos-actualizados', { detail: datos }))`, y el `Sidebar` lo escucha para actualizar el contador de préstamos por vencer. Lo mismo ocurre con `nodus:equipos-actualizados` para las garantías. Los nombres de los eventos están centralizados en `vencimientos.js` |
| **`localStorage`** | Solo para una preferencia del usuario: recordar el nombre de usuario en el login |

El servidor es la **fuente de verdad**: después de cada alta, modificación o baja la pantalla vuelve a pedir la lista (`limpiar(); cargarMarcas();`) en lugar de mantener una copia local sincronizada a mano. Se eligió simplicidad sobre optimización, lo que es razonable dado el volumen de datos de un hospital.

`vencimientos.js` concentra la lógica de negocio del cliente (días para vencer, días de atraso con prórrogas, garantías por vencer), para que **el menú y las pantallas muestren siempre el mismo número**. Las funciones son puras y no dependen de React, por lo que se pueden probar de forma aislada.

---

## 4. Comunicación y seguridad

### 4.1 Interacción frontend ↔ backend

- **Protocolo**: HTTP con el estilo arquitectónico REST. Recursos identificados por URL (`/api/equipos/5`) y operaciones expresadas con verbos HTTP.
- **Formato**: JSON en ambos sentidos. ASP.NET Core serializa con `System.Text.Json` usando *camelCase* por defecto (`IdPrestamo` → `idPrestamo`), que es la convención que espera el código JavaScript (`p.fechaEstimadaDevolucion`, `m.idMarca`). La excepción son la carga de escaneos firmados (`multipart/form-data` → `IFormFile` en `RemitosController`) y la descarga de PDFs (`application/pdf` con `PhysicalFile`).
- **Sin estado (*stateless*)**: el servidor no guarda sesión. Cada petición es independiente, que es justamente lo que permite autenticar con un JWT autocontenido.
- **Códigos de estado** con su significado estándar: `200`, `201 Created` (con cabecera `Location`), `204 No Content`, `400` (validación de negocio), `401` (credenciales inválidas) y `404`.
- **Documentación**: Swagger/OpenAPI se genera automáticamente en desarrollo (`/swagger`). `CustomSchemaIds` antepone el nombre del ensamblado a cada esquema para evitar colisiones entre clases con el mismo nombre en `NODUS.Models` y `NODUS.API.DTOs`.

**CORS.** Frontend (Vite, `localhost:5173`) y backend (`localhost:5085`) tienen orígenes distintos, así que el navegador bloquearía las peticiones sin una política CORS explícita. `Program.cs` define la política `ReactApp`:

```csharp
policy.SetIsOriginAllowed(origin =>
        origin == "http://localhost:3000" ||
        origin == "http://localhost:5173" ||
        origin == "http://localhost:5174" ||
        origin.EndsWith(".devtunnels.ms"))      // demos remotas con Dev Tunnels de Visual Studio
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials();
```

Es una lista blanca de orígenes, no un comodín `*`. `UseCors` va antes de `UseAuthentication` para que las peticiones *preflight* (`OPTIONS`) se respondan antes de cualquier verificación de identidad.

### 4.2 Control de acceso basado en roles (RBAC): `permisos.js`

`src/permisos.js` implementa un modelo **RBAC** centralizado. Su propio encabezado lo dice: *"Sistema centralizado de permisos por rol. Para dar o sacar un acceso, modificar solo este archivo."*

Define dos granularidades.

**a) Permisos por módulo (`ACCESO_MODULOS`)**: matriz rol → módulos visibles.

| Rol | patrimonial | mantenimiento | insumos | préstamos | reportes | administración |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| Administrador | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| Jefe patrimonio | ✔ | ✔ | ✔ | ✔ | ✔ | |
| Técnico biomédico | ✔ | ✔ | | ✔ | ✔ | |
| Jefe servicio | ✔ | ✔ | | | ✔ | |
| Médico / Residente / Enfermero | | ✔ | | | | |
| Auditor ANMAT | | | | | ✔ | |

**b) Permisos por acción (`ACCIONES`)**: operaciones puntuales dentro de un módulo: `crear_ot` (todos los roles, porque cualquier usuario puede reportar una falla), `editar_activo` (Administrador, Jefe patrimonio, Técnico biomédico) y `gestionar_usuarios` (solo Administrador).

Dos funciones puras sirven de interfaz: `puedeVerModulo(rol, modulo)` y `puedeHacer(rol, accion)`. Ambas devuelven `false` si el rol o el permiso no existen (`?.includes(...) ?? false`), así que **por defecto se deniega** (*deny by default*).

Dónde se consultan:

1. `Sidebar` (`App.jsx`): muestra u oculta cada grupo del menú.
2. `RutaProtegida` (`App.jsx`): bloquea la navegación directa por URL.
3. Dentro de las páginas: por ejemplo `FichaEquipo.jsx` usa `puedeHacer(rol, 'crear_ot')` para habilitar el alta de OT.

Centralizarlo tiene dos ventajas: la política de acceso **se audita en un solo archivo**, y cambiarla no obliga a tocar componentes.

> Todavía quedan dos reglas fuera de la matriz: en `Sidebar`, el enlace a *Bajas patrimoniales* se condiciona con `rol === 'Administrador' || rol === 'Jefe patrimonio'`. Convendría pasarla a `ACCIONES` (por ejemplo `'dar_baja'`) para mantener la regla de "un solo archivo".

**Alcance del RBAC actual.** Este control es de **presentación**: mejora la usabilidad y guía al usuario, pero se ejecuta en el navegador, que es un entorno que controla el propio usuario. Como la API todavía no exige token ni rol (§2.8), alguien con conocimientos técnicos podría llamar a los endpoints directamente. En un sistema en producción, **la autorización tiene que repetirse en el servidor**. Como el JWT ya lleva el claim de rol, esto se puede agregar sin cambiar el diseño (ver §6).

---

## 5. Diagrama de arquitectura

### 5.1 Vista en capas

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│  CAPA DE PRESENTACIÓN — Navegador web                                           │
│  nodus-frontend (React 19 + Vite 8 + React Router 7)                            │
│                                                                                 │
│   ┌──────────┐   ┌───────────────────────────────┐   ┌───────────────────────┐  │
│   │ Login    │──►│ App.jsx  (estado auth)        │──►│ pages/*.jsx           │  │
│   │ .jsx     │   │  ├─ Sidebar  ─┐               │   │ (useState/useEffect)  │  │
│   └────┬─────┘   │  └─ RutaProtegida ─┐          │   └──────────┬────────────┘  │
│        │         └──────────────┬─────┼──────────┘              │               │
│        │                        ▼     ▼                          │               │
│        │               ┌──────────────────────┐                  │               │
│        │               │ permisos.js  (RBAC)  │                  │               │
│        │               │ puedeVerModulo()     │                  │               │
│        │               │ puedeHacer()         │                  │               │
│        │               └──────────────────────┘                  │               │
│        ▼                                                         ▼               │
│   ┌─────────────────────────────────────────────────────────────────────────┐   │
│   │ api.js — instancia Axios  (baseURL = VITE_API_URL + "/api")             │   │
│   │ [JWT] ← token recibido en el login, guardado en estado `auth`           │   │
│   └─────────────────────────────────┬───────────────────────────────────────┘   │
└─────────────────────────────────────┼───────────────────────────────────────────┘
                                      │  HTTP/REST · JSON · CORS
                                      │  POST /api/auth/login  → { token, usuario }
                                      │  GET/POST/PUT/DELETE /api/...
                                      │  [Authorization: Bearer <JWT>] (previsto)
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  CAPA DE SERVICIOS — NODUS.API (ASP.NET Core Web API, .NET 10)                  │
│                                                                                 │
│   Pipeline:  UseCors("ReactApp") → UseAuthentication (JwtBearer) →              │
│              UseAuthorization → MapControllers                                  │
│                                                                                 │
│   ┌──────────────────────┐   ┌────────────────────────────────────────────┐     │
│   │ AuthController       │   │ Controllers REST (19)                      │     │
│   │  BCrypt.Verify       │   │  Equipos, Prestamos, OrdenesTrabajo, ...   │     │
│   │  GenerarToken()      │   │  [Route("api/[controller]")] [ApiController]│    │
│   │  HMAC-SHA256, 480min │   └───────┬───────────────────────┬────────────┘     │
│   │  claims: id, name,   │           │ DTOs (entrada/salida) │                  │
│   │          role, email │           ▼                       ▼                  │
│   └──────────┬───────────┘   ┌───────────────┐   ┌──────────────────────────┐   │
│              │               │ DTOs/         │   │ Services/                │   │
│              │               │ PrestamoDto   │   │ RemitoService (QuestPDF) │──►│── Storage/ (PDFs)
│              │               │ EquipoInputDto│   │ AlmacenamientoService    │   │
│              │               └───────────────┘   │ GeocodingService ────────┼──►│── Nominatim (OSM)
│              │                                   └──────────────────────────┘   │
└──────────────┼──────────────────────────┬───────────────────────────────────────┘
               │   Inyección de dependencias (DbContext scoped por petición)
               ▼                          ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  CAPA DE ACCESO A DATOS — NODUS.Data (EF Core 10 + Npgsql)                      │
│   NodusDbContext : DbContext                                                    │
│    ├─ 21 DbSet<T>  (Unit of Work + Repository)                                  │
│    ├─ OnModelCreating → Fluent API (tablas snake_case, FK, índices únicos,      │
│    │                     Restrict/Cascade, HasData seed)                        │
│    └─ Migrations/ (13 migraciones Code-First) + NodusDbContextFactory           │
│                                  ▲                                              │
│                                  │ usa                                          │
│  CAPA DE DOMINIO — NODUS.Models (POCOs: Equipo, Prestamo, Usuario, ...)         │
└──────────────────────────────────┬──────────────────────────────────────────────┘
                                   │  LINQ → SQL parametrizado · transacciones
                                   ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  CAPA DE PERSISTENCIA — PostgreSQL  (base nodus_db, puerto 5432)                │
│   equipo, prestamo, orden_trabajo, intervencion, usuario (password_hash BCrypt),│
│   hospital_externo, remito_contador (UPSERT ... ON CONFLICT), ...               │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Flujo de autenticación JWT (secuencia)

```
 Usuario        Login.jsx / App.jsx          NODUS.API                    PostgreSQL
    │                  │                          │                            │
    │ usuario+clave    │                          │                            │
    │─────────────────►│ POST /api/auth/login     │                            │
    │                  │ {usuario, password}      │                            │
    │                  │─────────────────────────►│ SELECT usuario WHERE       │
    │                  │                          │ nombre=? OR email=?        │
    │                  │                          │───────────────────────────►│
    │                  │                          │◄───────────────────────────│
    │                  │                          │ BCrypt.Verify(pwd, hash)   │
    │                  │                          │ GenerarToken():            │
    │                  │                          │  claims {id,name,ROLE,email}│
    │                  │                          │  firma HMAC-SHA256         │
    │                  │                          │  exp = ahora + 480 min     │
    │                  │ 200 {token, usuario}     │                            │
    │                  │◄─────────────────────────│                            │
    │                  │ setAuth(data)            │                            │
    │                  │ ─ permisos.js decide     │                            │
    │                  │   menú y rutas por rol   │                            │
    │  navega          │                          │                            │
    │─────────────────►│ GET /api/prestamos       │                            │
    │                  │ Authorization: Bearer JWT│  (previsto, ver §6)        │
    │                  │─────────────────────────►│ JwtBearer valida:          │
    │                  │                          │  firma, emisor, audiencia, │
    │                  │                          │  vigencia → ClaimsPrincipal│
    │                  │                          │ [Authorize(Roles=...)]     │
    │                  │                          │ LINQ → SQL                 │
    │                  │                          │───────────────────────────►│
    │                  │                          │◄───────────────────────────│
    │                  │ 200 [PrestamoDto...]     │ ToDto()                    │
    │                  │◄─────────────────────────│                            │
```

### 5.3 Descripción textual del diagrama (para acompañar la figura en la tesis)

> El sistema NODUS sigue una arquitectura cliente-servidor de tres niveles. En el **nivel de presentación**, una aplicación de página única desarrollada en React y empaquetada con Vite se ejecuta en el navegador del usuario. Su componente raíz administra el estado de sesión y, a través del módulo centralizado `permisos.js`, decide qué módulos del menú y qué rutas puede ver cada rol. Toda la comunicación con el servidor pasa por un único cliente HTTP (`api.js`), que intercambia mensajes JSON con la API mediante el estilo REST.
>
> En el **nivel de lógica**, una API desarrollada en ASP.NET Core sobre .NET 10 recibe las peticiones a través de un *pipeline* de middlewares (CORS, autenticación y autorización) y las despacha a controllers REST organizados por recurso. Los controllers usan objetos de transferencia de datos (DTO) para desacoplar el contrato público del modelo de persistencia y delegan la lógica transversal (generación de remitos en PDF, almacenamiento de archivos, geocodificación) en servicios de aplicación registrados en el contenedor de inyección de dependencias.
>
> La **capa de acceso a datos** usa Entity Framework Core con enfoque Code-First: las entidades del dominio son clases POCO definidas en un proyecto independiente, y su correspondencia con el esquema relacional se declara con la Fluent API en `NodusDbContext`. La evolución del esquema se versiona con migraciones generadas y aplicadas mediante la CLI de EF Core. En el **nivel de datos**, un servidor PostgreSQL guarda la información de forma persistente.
>
> La **autenticación** atraviesa todos los niveles mediante JSON Web Tokens. El servidor emite un token firmado con HMAC-SHA256, con el identificador, el nombre y el rol del usuario, después de verificar la contraseña contra su hash BCrypt. El cliente usa el rol para adaptar la interfaz y el servidor está configurado para validar la firma, el emisor, la audiencia y la vigencia del token en cada petición.

---

## 6. Brechas detectadas y mejoras recomendadas

Al recorrer el código aparecieron diferencias entre el diseño previsto y lo que está implementado hoy. Conviene resolverlas antes de la defensa, o presentarlas explícitamente como trabajo futuro.

| # | Situación actual | Riesgo | Cambio sugerido |
|---|---|---|---|
| 1 | Ningún controller exige `[Authorize]` | Cualquiera que llegue a la API puede leer o modificar datos sin iniciar sesión | Política global en `Program.cs`: `builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());` y `[AllowAnonymous]` en `AuthController.Login` |
| 2 | `api.js` no envía el token | Aunque se active el punto 1, el frontend recibiría `401` | Interceptor: `api.interceptors.request.use(cfg => { if (token) cfg.headers.Authorization = \`Bearer ${token}\`; return cfg; })`, con el token de `auth.token`. Agregar también un interceptor de respuesta que ante un `401` cierre la sesión |
| 3 | El RBAC solo existe en el cliente | Se puede saltear llamando a la API directamente | Replicar la matriz de `permisos.js` con `[Authorize(Roles = "Administrador")]` en `UsuariosController`, `[Authorize(Roles = "Administrador,Jefe patrimonio")]` en `BajasPatrimonialesController`, etc., o con políticas nombradas por módulo |
| 4 | `GET /api/auth/hash/{password}` es público | Endpoint de utilidad de desarrollo expuesto; además pone la contraseña en la URL (logs, historial) | Eliminarlo o restringirlo a `IsDevelopment()` |
| 5 | El `idUsuario` que registra una operación viaja en el cuerpo (`Prestamos.jsx`: `auth?.usuario?.idUsuario \|\| 1`) | El cliente puede atribuir una operación a otro usuario; si falta, se usa el id 1 | Obtenerlo en el servidor desde el token: `int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)` |
| 6 | Clave JWT y contraseña de PostgreSQL en `appsettings.json` y `NodusDbContextFactory.cs` | Secretos versionados en el repositorio | *User Secrets* en desarrollo (`dotnet user-secrets set "Jwt:Key" ...`) y variables de entorno en producción |
| 7 | Sesión solo en memoria | Recargar la página cierra la sesión | Guardar el token en `sessionStorage` y restaurarlo al iniciar, validando `exp` |
| 8 | `NODUS.Services` está vacío y los servicios viven en `NODUS.API/Services` | La estructura de la solución promete una capa que no existe | Mover `RemitoService` y la lógica de negocio de préstamos a `NODUS.Services`, o quitar el proyecto y documentar la decisión |
| 9 | `axios` y `@tabler/icons-react` faltan en `package.json` | `npm install` + `npm run build` falla en una máquina limpia | `npm install axios @tabler/icons-react` |

Con los cambios 1 a 3, el diagrama de la §5.2 pasa a describir exactamente el comportamiento del sistema, y el control de acceso queda aplicado en el servidor, que es donde está la garantía de seguridad.
