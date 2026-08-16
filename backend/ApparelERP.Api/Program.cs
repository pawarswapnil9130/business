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

// Determine Database Provider (Sqlite or PostgreSql)
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
Console.WriteLine($"[Database Engine] Active provider: {dbProvider}");

if (dbProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) || dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("PostgresConnection") 
                           ?? builder.Configuration.GetConnectionString("DefaultConnection");

    // Force IPv4 address resolution (Fixes Supabase/Cloud direct connection IPv6 timeout on IPv4-only networks)
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
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[IPv4 DNS Helper] Warning: {ex.Message}");
    }

    builder.Services.AddDbContext<ApparelDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    // Default to local SQLite database (Desktop / Offline mode)
    var sqliteConnection = builder.Configuration.GetConnectionString("SqliteConnection") 
                           ?? builder.Configuration.GetConnectionString("DefaultConnection") 
                           ?? "Data Source=ApparelERP.db";
    Console.WriteLine($"[SQLite Database] Using local database: {sqliteConnection}");

    builder.Services.AddDbContext<ApparelDbContext>(options =>
        options.UseSqlite(sqliteConnection));
}

// Register Custom Services for DI
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ICostingService, CostingService>();
builder.Services.AddScoped<ISalesService, SalesService>();

// Enable CORS for frontend development
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

// Configure Swashbuckle Swagger
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

var app = builder.Build();

// Database Initialization & Super Admin Seed
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApparelDbContext>();
        
        if (dbProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) || dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[Startup Health Check] Testing Postgres database connection...");
            if (dbContext.Database.CanConnect())
            {
                Console.WriteLine("[Startup Health Check] Database connected successfully!");
                try
                {
                    dbContext.Database.ExecuteSqlRaw("ALTER TABLE users DROP CONSTRAINT IF EXISTS users_role_check;");
                    dbContext.Database.ExecuteSqlRaw("ALTER TABLE users DROP CONSTRAINT IF EXISTS users_role_check1;");
                    dbContext.Database.ExecuteSqlRaw("ALTER TABLE users DROP CONSTRAINT IF EXISTS users_role_check2;");
                    dbContext.Database.ExecuteSqlRaw("ALTER TABLE production_batches ADD COLUMN IF NOT EXISTS design_name VARCHAR(255);");
                    dbContext.Database.ExecuteSqlRaw("ALTER TABLE production_batches ALTER COLUMN product_id DROP NOT NULL;");
                    dbContext.Database.ExecuteSqlRaw("ALTER TABLE production_batches ADD COLUMN IF NOT EXISTS quantity_to_sew INT DEFAULT 0;");
                    dbContext.Database.ExecuteSqlRaw("ALTER TABLE fabrics ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN DEFAULT FALSE;");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Startup Db Helper] Warning: {ex.Message}");
                }
            }
        }
        else
        {
            // SQLite Initialization
            Console.WriteLine("[Startup Db Helper] Initializing local SQLite database tables & views...");
            dbContext.Database.EnsureCreated();

            // Create SQLite Views
            var createStockViewSql = @"
                CREATE VIEW IF NOT EXISTS vw_stock_summary AS
                SELECT 
                    p.id AS product_id,
                    p.name AS product_name,
                    p.category,
                    p.product_type,
                    p.design_brand,
                    p.size,
                    p.color,
                    p.cost_price,
                    p.selling_price,
                    COALESCE(SUM(sl.quantity_change), 0) AS current_stock
                FROM products p
                LEFT JOIN stock_ledger sl ON p.id = sl.product_id
                GROUP BY p.id, p.name, p.category, p.product_type, p.design_brand, p.size, p.color, p.cost_price, p.selling_price;
            ";
            dbContext.Database.ExecuteSqlRaw(createStockViewSql);

            var createProfitViewSql = @"
                CREATE VIEW IF NOT EXISTS vw_profit_report AS
                SELECT 
                    so.id AS sales_order_id,
                    so.invoice_no,
                    so.customer_name,
                    so.sales_date,
                    p.id AS product_id,
                    p.name AS product_name,
                    p.product_type,
                    p.category,
                    p.size,
                    p.color,
                    soi.item_type,
                    soi.quantity AS quantity_sold,
                    soi.unit_price AS selling_price_per_unit,
                    soi.sub_total AS item_revenue,
                    p.cost_price AS cost_price_per_unit,
                    (p.cost_price * (CASE WHEN soi.item_type = 'SET' THEN soi.quantity * 4 ELSE soi.quantity END)) AS total_cost_basis,
                    (soi.sub_total - (soi.gst_percent / 100.0 * soi.sub_total) - (p.cost_price * (CASE WHEN soi.item_type = 'SET' THEN soi.quantity * 4 ELSE soi.quantity END))) AS net_profit
                FROM sales_order_items soi
                JOIN sales_orders so ON soi.sales_order_id = so.id
                JOIN products p ON soi.product_id = p.id;
            ";
            dbContext.Database.ExecuteSqlRaw(createProfitViewSql);
            Console.WriteLine("[Startup Db Helper] SQLite views configured successfully!");
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
            Console.WriteLine("[Startup Seed] Default super admin created: 'admin' / 'Reset@123'");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup Seed Error] {ex.Message}");
    }
}

// Configure Swagger for API testing
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("EnableSwagger", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Apparel ERP API v1");
    });
}

// Serve Angular Static Files from wwwroot (Desktop SPA mode)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// SPA Fallback for Angular client-side routes
app.MapFallbackToFile("index.html");

app.Run();
