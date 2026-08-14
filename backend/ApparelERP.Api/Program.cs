using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ApparelERP.Api.Data;
using ApparelERP.Api.Models;
using ApparelERP.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Configure EF Core with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Force IPv4 address resolution (Fixes Supabase direct connection IPv6 timeout on IPv4-only networks)
try
{
    Console.WriteLine($"[IPv4 DNS Helper] Original connection string: {connectionString}");
    var connBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    Console.WriteLine($"[IPv4 DNS Helper] Parsed Host: {connBuilder.Host}");
    if (!string.IsNullOrEmpty(connBuilder.Host) && !connBuilder.Host.Contains("neon.tech") && !System.Net.IPAddress.TryParse(connBuilder.Host, out _))
    {
        Console.WriteLine($"[IPv4 DNS Helper] Resolving DNS for {connBuilder.Host}...");
        var ipAddresses = System.Net.Dns.GetHostAddresses(connBuilder.Host);
        Console.WriteLine($"[IPv4 DNS Helper] Resolved {ipAddresses.Length} addresses.");
        var ipv4 = ipAddresses.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
        if (ipv4 != null)
        {
            Console.WriteLine($"[IPv4 DNS Helper] Forcing IPv4: {ipv4}");
            connBuilder.Host = ipv4.ToString();
            connectionString = connBuilder.ConnectionString;
        }
        else
        {
            Console.WriteLine("[IPv4 DNS Helper] No IPv4 address found in DNS records.");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[IPv4 DNS Helper] Failed to force IPv4 connection: {ex.Message}");
}

builder.Services.AddDbContext<ApparelDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register Custom Services for DI
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ICostingService, CostingService>();
builder.Services.AddScoped<ISalesService, SalesService>();

// Enable CORS for frontend integration (Angular CLI runs on port 4200 by default)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure JWT Authentication
var secretKey = builder.Configuration["JwtSettings:Secret"] ?? "SuperSecretKeyForApparelERPSystem_2026_SecureKey_MustBeLongEnough";
var key = Encoding.ASCII.GetBytes(secretKey);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
      };
});

// Configure Swashbuckle Swagger with JWT Authorize support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo { Title = "Apparel ERP API", Version = "v1" });
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid JWT Token (without writing the prefix 'Bearer ')",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });

    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Swashbuckle handles OpenAPI specification generation.

var app = builder.Build();

// Seed Super Admin on Startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApparelDbContext>();
        
        Console.WriteLine("[Startup Health Check] Testing database connection...");
        if (dbContext.Database.CanConnect())
        {
            Console.WriteLine("[Startup Health Check] Database connected successfully!");


            // Drop Postgres check constraint to allow new roles (ADMIN, EMPLOYEE, CA)
            try
            {
                Console.WriteLine("[Startup Db Helper] Dropping users check constraints to allow new roles (ADMIN, EMPLOYEE, CA)...");
                dbContext.Database.ExecuteSqlRaw("ALTER TABLE users DROP CONSTRAINT IF EXISTS users_role_check;");
                dbContext.Database.ExecuteSqlRaw("ALTER TABLE users DROP CONSTRAINT IF EXISTS users_role_check1;");
                dbContext.Database.ExecuteSqlRaw("ALTER TABLE users DROP CONSTRAINT IF EXISTS users_role_check2;");
                
                Console.WriteLine("[Startup Db Helper] Modifying production_batches columns for custom designs and planned quantity...");
                dbContext.Database.ExecuteSqlRaw("ALTER TABLE production_batches ADD COLUMN IF NOT EXISTS design_name VARCHAR(255);");
                dbContext.Database.ExecuteSqlRaw("ALTER TABLE production_batches ALTER COLUMN product_id DROP NOT NULL;");
                dbContext.Database.ExecuteSqlRaw("ALTER TABLE production_batches ADD COLUMN IF NOT EXISTS quantity_to_sew INT DEFAULT 0;");
                
                Console.WriteLine("[Startup Db Helper] Adding is_deleted column to fabrics...");
                dbContext.Database.ExecuteSqlRaw("ALTER TABLE fabrics ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN DEFAULT FALSE;");
                


                Console.WriteLine("[Startup Db Helper] Constraints adjusted successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup Db Helper] Warning: could not drop roles check constraint: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("[Startup Health Check] Failed to connect to the database.");
        }

        // Seed Super Admin if not existing
        var adminUser = dbContext.Users.FirstOrDefault(u => u.Username.ToLower() == "admin");
        if (adminUser == null)
        {
            dbContext.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Reset@123"),
                Role = "SUPER_ADMIN",
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            });
            dbContext.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error seeding data: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Apparel ERP API v1");
    });
}

app.UseCors();

// Authentication middleware must be before Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map controllers for endpoints
app.MapControllers();

app.Run();
