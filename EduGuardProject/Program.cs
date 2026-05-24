using EduGuardProject.Models;
using EduGuardProject.Repositories;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ================= DATABASE =================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<AppRole>("app_role");
dataSourceBuilder.MapEnum<BillingModel>("billing_model_enum");
dataSourceBuilder.MapEnum<InstitutionStatus>("institution_status");
dataSourceBuilder.MapEnum<UserStatus>("user_status");
dataSourceBuilder.MapEnum<TransactionType>("transaction_type");
dataSourceBuilder.MapEnum<TransactionStatus>("transaction_status");
dataSourceBuilder.MapEnum<PricingServiceType>("pricing_service_type");
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource, npgsqlOptions => npgsqlOptions.UseVector()));

// Đăng ký Supabase Client
var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:Key"];
var supabaseOptions = new Supabase.SupabaseOptions { AutoConnectRealtime = true };
builder.Services.AddScoped<Supabase.Client>(_ => new Supabase.Client(supabaseUrl, supabaseKey, supabaseOptions));

// Đăng ký DI (Repositories)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IInstitutionRepository, InstitutionRepository>();
builder.Services.AddScoped<IPricingConfigRepository, PricingConfigRepository>();

// Đăng ký Service

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInstitutionService, InstitutionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPricingConfigService, PricingConfigService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ================= CẤU HÌNH XÁC THỰC JWT SUPABASE (ĐÃ SỬA CHUẨN ĐÉT) =================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Sử dụng chính cái ServiceRoleKey dài ngoằng trong appsettings của bạn để làm chìa khóa giải mã chữ ký JWT
    var jwtSecret = builder.Configuration["Supabase:ServiceRoleKey"];

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,          // Tắt vì Supabase dùng issuer động
        ValidateAudience = false,        // Tắt check Audience để tránh lỗi lệch cấu hình
        ValidateLifetime = true,         // Bật check hạn sử dụng của Token
        ValidateIssuerSigningKey = true,   // Bắt buộc kiểm tra chữ ký bảo mật xem có đúng Supabase ký không
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret!)),
        ClockSkew = TimeSpan.Zero
    };
});

// ================= SERVICES & SWAGGER =================
builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });

builder.Services.AddEndpointsApiExplorer();

// Cấu hình ổ khóa bảo mật xịn cho Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "EduGuard API", Version = "v1" });

    // ================= CẤU HÌNH LẠI ĐOẠN Ổ KHÓA Ở ĐÂY =================
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Chỉ cần dán thẳng mã Token của bạn vào đây",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http, 
        Scheme = "Bearer",                                       
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => { policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin(); });
});

var app = builder.Build();

// ================= PIPELINE MIDDLEWARE =================
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseCors();

// 🚨 Đảm bảo Authentication phải chạy trước Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();