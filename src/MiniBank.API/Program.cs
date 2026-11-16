using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;
using MiniBank.Common;
using MiniBank.Data;
using MiniBank.Data.Repositories;
using MiniBank.Services;
using MiniBank.Services.Generators;
using MiniBank.Services.Security;
using Scalar.AspNetCore;
using Serilog;
using System.Text;

// 1. Erken aşama loglayıcı (Uygulama ayağa kalkarken oluşabilecek hataları yakalamak için)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("MiniBank API başlatılıyor...");

    // ASP.NET Core WebApplicationBuilder derleme sırasında wwwroot klasörünün varlığını bekler
    var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    if (!Directory.Exists(wwwrootPath))
    {
        Directory.CreateDirectory(wwwrootPath);
    }

    var builder = WebApplication.CreateBuilder(args);

    // 2. Serilog Entegrasyonu (Ayarları appsettings.json dosyasından okur)
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // 3. PostgreSQL & DbContext Kaydı
    builder.Services.AddDbContext<MiniBankDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // 4. Strongly-Typed Configuration (Options Pattern - DIP)
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

    // 5. Cross-Cutting & Security Services (Single Responsibility & Dependency Inversion)
    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
    builder.Services.AddScoped<ITokenService, JwtTokenService>();
    builder.Services.AddSingleton<IBankingCodeGenerator, BankingCodeGenerator>();

    // 6. Data Access Layer (DAL) Kayıtları (Repository & UnitOfWork - LSP, DIP)
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
    builder.Services.AddScoped<IAccountRepository, AccountRepository>();
    builder.Services.AddScoped<IAdminRepository, AdminRepository>();
    builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
    builder.Services.AddScoped<IDbInitializer, DbInitializer>();

    // 7. Business Logic Layer (BLL) Servis Kayıtları (Single Responsibility & Interface Segregation)
    builder.Services.AddScoped<ICurrencyService, CurrencyService>();
    builder.Services.AddScoped<ITimeDepositService, TimeDepositService>();
    builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
    builder.Services.AddScoped<IUserProfileService, UserProfileService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<ITransactionService, TransactionService>();
    builder.Services.AddScoped<IAdminService, AdminService>();
    builder.Services.AddScoped<IFeatureManagementService, FeatureManagementService>();
    builder.Services.AddScoped<IAssistantService, AssistantService>();
    builder.Services.AddScoped<ICardService, CardService>();
    builder.Services.AddScoped<IDashboardService, DashboardService>();
    builder.Services.AddScoped<ISpendingService, SpendingService>();

    // 8. JWT Kimlik Doğrulama (Authentication) Yapılandırması
    var jwtKey = builder.Configuration["Jwt:Key"] ?? "MiniBankSuperSecretKeyForJwtAuthentication2026!+MustBeLongEnough";
    var issuer = builder.Configuration["Jwt:Issuer"] ?? "MiniBankServer";
    var audience = builder.Configuration["Jwt:Audience"] ?? "MiniBankClient";

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

    // 6. Controller ve FluentValidation Entegrasyonu
    builder.Services.AddControllers();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssembly(typeof(MiniBank.Services.AccountService).Assembly);

    // 7. OpenAPI Dokümantasyonu (.NET yerel OpenAPI desteği)
    builder.Services.AddOpenApi();

    // 8. CORS Yapılandırması (React Frontend'in API'ye erişebilmesi için)
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowReactApp", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    // 9. Otomatik Tablo Oluşturma ve Başlangıç Verisi (Seed Data)
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<MiniBankDbContext>();

            // Veritabanı durumunu ve yeni tabloları kontrol et; şema uyuşmuyorsa kolon ve tabloları güvenle ekle
            try
            {
                context.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ""Admins"" (
                        ""Id"" uuid NOT NULL,
                        ""Username"" character varying(50) NOT NULL,
                        ""FullName"" character varying(100) NOT NULL,
                        ""Email"" character varying(100) NOT NULL,
                        ""PhoneNumber"" character varying(20) NOT NULL,
                        ""PasswordHash"" bytea NOT NULL,
                        ""PasswordSalt"" bytea NOT NULL,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        CONSTRAINT ""PK_Admins"" PRIMARY KEY (""Id"")
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Admins_Username"" ON ""Admins"" (""Username"");
                    ALTER TABLE ""Customers"" ADD COLUMN IF NOT EXISTS ""Role"" character varying(20) DEFAULT 'Customer';
                    ALTER TABLE ""Customers"" ADD COLUMN IF NOT EXISTS ""PasswordHash"" bytea NULL;
                    ALTER TABLE ""Customers"" ADD COLUMN IF NOT EXISTS ""PasswordSalt"" bytea NULL;
                    ALTER TABLE ""Customers"" ALTER COLUMN ""IdentityNumber"" TYPE character varying(20);
                    ALTER TABLE ""Accounts"" ADD COLUMN IF NOT EXISTS ""AccountType"" character varying(20) DEFAULT 'Demand';
                    ALTER TABLE ""Accounts"" ADD COLUMN IF NOT EXISTS ""MaturityDays"" integer NULL;
                    ALTER TABLE ""Accounts"" ADD COLUMN IF NOT EXISTS ""InterestRate"" numeric(5,2) NULL;
                    ALTER TABLE ""Accounts"" ADD COLUMN IF NOT EXISTS ""MaturityDate"" timestamp with time zone NULL;
                    ALTER TABLE ""Accounts"" ADD COLUMN IF NOT EXISTS ""InitialDepositAmount"" numeric(18,2) NULL;
                    ALTER TABLE ""Accounts"" ADD COLUMN IF NOT EXISTS ""ExpectedInterestAmount"" numeric(18,2) NULL;
                    ALTER TABLE ""Accounts"" ADD COLUMN IF NOT EXISTS ""IsMatured"" boolean DEFAULT false;
                    ALTER TABLE ""Accounts"" ADD COLUMN IF NOT EXISTS ""IsClosed"" boolean DEFAULT false;
                    ALTER TABLE ""AccountTransactions"" ADD COLUMN IF NOT EXISTS ""Category"" character varying(50) DEFAULT 'Diğer';
                    CREATE TABLE IF NOT EXISTS ""CustomerFeatures"" (
                        ""Id"" uuid NOT NULL,
                        ""CustomerId"" uuid NOT NULL,
                        ""FeatureKey"" character varying(50) NOT NULL,
                        ""IsEnabled"" boolean NOT NULL DEFAULT true,
                        ""CustomValue"" character varying(100) NULL,
                        ""UpdatedAt"" timestamp with time zone NOT NULL,
                        ""UpdatedBy"" character varying(50) NULL,
                        CONSTRAINT ""PK_CustomerFeatures"" PRIMARY KEY (""Id""),
                        CONSTRAINT ""FK_CustomerFeatures_Customers"" FOREIGN KEY (""CustomerId"") REFERENCES ""Customers"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CustomerFeatures_CustomerId_FeatureKey"" ON ""CustomerFeatures"" (""CustomerId"", ""FeatureKey"");
                    CREATE TABLE IF NOT EXISTS ""Cards"" (
                        ""Id"" uuid NOT NULL,
                        ""CustomerId"" uuid NOT NULL,
                        ""AccountId"" uuid NOT NULL,
                        ""CardNumber"" character varying(19) NOT NULL,
                        ""CardHolderName"" character varying(100) NOT NULL,
                        ""ExpiryMonth"" integer NOT NULL,
                        ""ExpiryYear"" integer NOT NULL,
                        ""Cvv"" character varying(4) NOT NULL,
                        ""CardType"" character varying(20) NOT NULL DEFAULT 'Debit',
                        ""Status"" integer NOT NULL DEFAULT 1,
                        ""IsInternationalAllowed"" boolean NOT NULL DEFAULT true,
                        ""IsOnlineShoppingAllowed"" boolean NOT NULL DEFAULT true,
                        ""IsContactlessAllowed"" boolean NOT NULL DEFAULT true,
                        ""DailyLimit"" numeric(18,2) NOT NULL DEFAULT 15000.00,
                        ""DailyAtmWithdrawLimit"" numeric(18,2) NOT NULL DEFAULT 5000.00,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""UpdatedAt"" timestamp with time zone NULL,
                        CONSTRAINT ""PK_Cards"" PRIMARY KEY (""Id""),
                        CONSTRAINT ""FK_Cards_Customers"" FOREIGN KEY (""CustomerId"") REFERENCES ""Customers"" (""Id"") ON DELETE CASCADE,
                        CONSTRAINT ""FK_Cards_Accounts"" FOREIGN KEY (""AccountId"") REFERENCES ""Accounts"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Cards_CardNumber"" ON ""Cards"" (""CardNumber"");
                    CREATE INDEX IF NOT EXISTS ""IX_Cards_CustomerId"" ON ""Cards"" (""CustomerId"");
                    CREATE INDEX IF NOT EXISTS ""IX_Cards_AccountId"" ON ""Cards"" (""AccountId"");
                    DELETE FROM ""Cards"" WHERE ""AccountId"" IN (SELECT ""Id"" FROM ""Accounts"" WHERE ""AccountType"" != 'Demand' OR (""Currency"" != 'TRY' AND ""Currency"" != 'TL'));
                ");
            }
            catch (Exception sqlEx)
            {
                Log.Warning("Şema güncelleme uyarısı: {Msg}", sqlEx.Message);
            }

            // Eksik tohum verilerini ve hesabı olmayan müşterilerin hesaplarını oluştur (IDbInitializer - DIP)
            var dbInitializer = services.GetRequiredService<IDbInitializer>();
            await dbInitializer.SeedAsync();

            Log.Information("Veritabanı tabloları ve test verileri başarıyla hazırlandı.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Veritabanı kurulumunda bir hata oluştu: {Message}", ex.Message);
        }
    }

    // 9. HTTP Pipeline Yapılandırması
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(); // API Test Arayüzü: /scalar/v1
    }

    // CORS aktif
    app.UseCors("AllowReactApp");

    // Gelen her HTTP isteğini detaylı loglama (Süre, Status Code, Endpoint vb.)
    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    // JWT Kimlik Doğrulama ve Yetkilendirme Middleware'leri
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    Log.Information("==========================================================");
    Log.Information("🚀 MiniBank Backend Web API Hazır!");
    Log.Information("👉 Backend API URL: http://localhost:5257");
    Log.Information("👉 API Dokümantasyonu (Scalar): http://localhost:5257/scalar/v1");
    Log.Information("==========================================================");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MiniBank API beklenmeyen bir hata nedeniyle durduruldu!");
}
finally
{
    Log.Information("Loglar temizlenip uygulama sonlandırılıyor...");
    Log.CloseAndFlush();
}

