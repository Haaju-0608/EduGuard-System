using EduGuardProject.Hubs;
using EduGuardProject.Models;
using EduGuardProject.Repositories;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services;
using EduGuardProject.Services.IServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text;
using System.Text.Json.Serialization;

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
dataSourceBuilder.MapEnum<AttendanceMethod>("attendance_method");
dataSourceBuilder.MapEnum<AttendanceStatus>("attendance_status");
dataSourceBuilder.MapEnum<SessionStatus>("session_status");
dataSourceBuilder.MapEnum<EnrollmentStatus>("enrollment_status");
dataSourceBuilder.MapEnum<BiometricReqStatus>("biometric_req_status");
dataSourceBuilder.MapEnum<ParticipationStatus>("participation_status");
dataSourceBuilder.MapEnum<ExamSlotStatus>("exam_slot_status");
dataSourceBuilder.MapEnum<NotificationType>("notification_type");
dataSourceBuilder.MapEnum<NotificationChannel>("notification_channel");
dataSourceBuilder.MapEnum<ReferenceTypeEnum>("reference_type_enum");
dataSourceBuilder.MapEnum<ViolationSeverity>("violation_severity");
dataSourceBuilder.MapEnum<ViolationType>("violation_type");
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
builder.Services.AddScoped<IClassRepository, ClassRepository>();
builder.Services.AddScoped<IClassEnrollmentRepository, ClassEnrollmentRepository>();
builder.Services.AddScoped<IAttendanceSessionRepository, AttendanceSessionRepository>();
builder.Services.AddScoped<IAttendanceRecordRepository, AttendanceRecordRepository>();
builder.Services.AddScoped<IBiometricRequestRepository, BiometricRequestRepository>();
builder.Services.AddScoped<IBiometricDatumRepository, BiometricDatumRepository>();
builder.Services.AddScoped<IExamParticipationRepository, ExamParticipationRepository>();
builder.Services.AddScoped<IExamslotRepository, ExamSlotRepository>();
builder.Services.AddScoped<IViolationLogRepository, ViolationLogRepository>();

// Đăng ký Service
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInstitutionService, InstitutionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPricingConfigService, PricingConfigService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IClassEnrollmentService, ClassEnrollmentService>();
builder.Services.AddScoped<IAttendanceSessionService, AttendanceSessionService>();
builder.Services.AddScoped<IAttendanceRecordService, AttendanceRecordService>();
builder.Services.AddScoped<IBiometricRequestService, BiometricRequestService>();
builder.Services.AddScoped<IBiometricDatumService, BiometricDatumService>();
builder.Services.AddScoped<IExamParticipationService, ExamParticipationServices>();
builder.Services.AddScoped<IExamSlotServices, ExamslotServices>();
builder.Services.AddScoped<IViolationLogService, ViolationLogServices>();

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
builder.Services.AddSignalR();

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
app.MapHub<ChatHub>("/chatHub");
app.Run();