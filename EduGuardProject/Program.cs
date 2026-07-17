using EduGuardProject.Models;
using EduGuardProject.Repositories;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services;
using EduGuardProject.Services.BackgroundJobs;
using EduGuardProject.Services.Email;
using EduGuardProject.Services.IServices;
using EduGuardProject.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys")));

// Database
// Render (và nhiều PaaS) inject biến PORT — bind Kestrel trước khi Build.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

// ================= DATABASE =================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();
dataSourceBuilder.MapEnum<AppRole>("app_role");
dataSourceBuilder.MapEnum<BillingModel>("billing_model_enum");
dataSourceBuilder.MapEnum<InstitutionStatus>("institution_status");
dataSourceBuilder.MapEnum<UserStatus>("user_status");
dataSourceBuilder.MapEnum<TransactionType>("transaction_type");
dataSourceBuilder.MapEnum<TransactionStatus>("transaction_status");
dataSourceBuilder.MapEnum<PricingServiceType>("pricing_service_type");
dataSourceBuilder.MapEnum<NotificationType>("notification_type");
dataSourceBuilder.MapEnum<NotificationChannel>("notification_channel");
dataSourceBuilder.MapEnum<ReferenceTypeEnum>("reference_type_enum");
dataSourceBuilder.MapEnum<SessionStatus>("session_status");
dataSourceBuilder.MapEnum<AttendanceMethod>("attendance_method");
dataSourceBuilder.MapEnum<AttendanceStatus>("attendance_status");
dataSourceBuilder.MapEnum<BiometricReqStatus>("biometric_req_status");
dataSourceBuilder.MapEnum<EnrollmentStatus>("enrollment_status");
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
dataSourceBuilder.MapEnum<StudentExamRecordStatus>("student_exam_record_status");
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource, npgsqlOptions => npgsqlOptions.UseVector()));

// Supabase client
var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Supabase:Url is not configured.");
var supabaseKey = builder.Configuration["Supabase:Key"]
    ?? throw new InvalidOperationException("Supabase:Key is not configured.");
var supabaseOptions = new Supabase.SupabaseOptions { AutoConnectRealtime = true };
builder.Services.AddScoped<Supabase.Client>(_ => new Supabase.Client(supabaseUrl, supabaseKey, supabaseOptions));

// Repositories
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
builder.Services.AddScoped<IExamQuestionRepository, ExamQuestionRepository>();
builder.Services.AddScoped<IStudentExamRecordRepository, StudentExamRecordRepository>();
builder.Services.AddScoped<IViolationLogRepository, ViolationLogRepository>();

// Services
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
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
builder.Services.AddScoped<IExamQuestionService, ExamQuestionService>();
builder.Services.AddScoped<IStudentExamRecordService, StudentExamRecordService>();
builder.Services.AddScoped<IViolationLogService, ViolationLogServices>();
builder.Services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
builder.Services.AddScoped<IRealtimeEventDispatcher, RealtimeEventDispatcher>();
builder.Services.AddScoped<IExamWorkflowService, ExamWorkflowService>();
builder.Services.AddSingleton<IExamPresenceTracker, ExamPresenceTracker>();
builder.Services.AddScoped<IDashboardStatsService, DashboardStatsService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<EmailTemplateService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHttpClient<IAiServiceClient, AiServiceClient>();

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var authority = $"{supabaseUrl.TrimEnd('/')}/auth/v1";

    options.Authority = authority;
    options.Audience = "authenticated";
    options.RequireHttpsMetadata = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = authority,
        ValidateAudience = true,
        ValidAudience = "authenticated",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = "sub"
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = async context =>
        {
            var accessToken = context.Request.Query["access_token"].ToString();
            var path = context.HttpContext.Request.Path;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                var authorization = context.Request.Headers.Authorization.ToString();
                if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    accessToken = authorization["Bearer ".Length..].Trim();
            }

            if (string.IsNullOrWhiteSpace(accessToken))
                return;

            context.Token = accessToken;

            var userId = await ValidateSupabaseAccessTokenAsync(
                context.HttpContext,
                accessToken,
                supabaseUrl,
                supabaseKey,
                context.HttpContext.RequestAborted);

            if (!userId.HasValue)
            {
                context.Fail("Supabase access token is invalid or expired.");
                return;
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                new Claim("sub", userId.Value.ToString())
            };
            context.Principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme));
            context.Success();
        }
    };
});

// API and Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "EduGuard API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Dán JWT access token vào đây.",
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
builder.Services.AddHttpClient();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 525L * 1024 * 1024;
});
builder.Services.AddSignalR();
builder.Services.AddHostedService<ExamReminderBackgroundService>();
builder.Services.AddHostedService<LowBalanceBackgroundService>();
builder.Services.AddHostedService<StorageCleanupBackgroundService>();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();

        if (corsOrigins.Length > 0)
            policy.WithOrigins(corsOrigins).AllowCredentials();
        else
            policy.AllowAnyOrigin();
    });
});

var app = builder.Build();

// Middleware pipeline
app.UseSwagger();
app.UseSwaggerUI();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors();

// Authentication must run before authorization.
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();
app.MapGet("/", () => Results.File(Path.Combine(app.Environment.WebRootPath, "index.cshtml"), "text/html"));
app.MapGet("/index.cshtml", () => Results.File(Path.Combine(app.Environment.WebRootPath, "index.cshtml"), "text/html"));
app.MapHub<NotificationHub>(HubRoutes.Notifications).RequireAuthorization();
app.MapHub<ExamHub>(HubRoutes.Exams).RequireAuthorization();
app.MapHub<AttendanceHub>(HubRoutes.Attendance).RequireAuthorization();
app.MapHub<DashboardHub>(HubRoutes.Dashboard).RequireAuthorization();
app.Run();

static async Task<Guid?> ValidateSupabaseAccessTokenAsync(
    HttpContext httpContext,
    string accessToken,
    string supabaseUrl,
    string supabaseKey,
    CancellationToken cancellationToken)
{
    var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));
    var cache = httpContext.RequestServices.GetRequiredService<IMemoryCache>();
    if (cache.TryGetValue<Guid>($"supabase-token:{tokenHash}", out var cachedUserId))
        return cachedUserId;

    var httpClientFactory = httpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
    var client = httpClientFactory.CreateClient("SupabaseAuthValidation");

    using var request = new HttpRequestMessage(
        HttpMethod.Get,
        $"{supabaseUrl.TrimEnd('/')}/auth/v1/user");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    request.Headers.TryAddWithoutValidation("apikey", supabaseKey);

    using var response = await client.SendAsync(request, cancellationToken);
    if (!response.IsSuccessStatusCode)
        return null;

    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
    using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    if (!json.RootElement.TryGetProperty("id", out var idProperty) ||
        !Guid.TryParse(idProperty.GetString(), out var userId))
    {
        return null;
    }

    cache.Set($"supabase-token:{tokenHash}", userId, TimeSpan.FromMinutes(5));
    return userId;
}
