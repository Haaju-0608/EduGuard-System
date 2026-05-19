using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Repositories;
using Microsoft.EntityFrameworkCore;
using Supabase;
using EduGuardProject.Services.IServices;
using EduGuardProject.Services;
using Npgsql; // 👈 THÊM THƯ VIỆN NÀY VÀO ĐÂY NHÉ

var builder = WebApplication.CreateBuilder(args);

// ================= DATABASE =================

// 1. TẠO DATA SOURCE ĐỂ ĐĂNG KÝ ENUM "app_role" VỚI NPGSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<AppRole>("app_role"); // Đăng ký Enum ở đây
var dataSource = dataSourceBuilder.Build();

// 2. ĐĂNG KÝ DBCONTEXT (Dùng dataSource vừa tạo ở trên và giữ nguyên UseVector của bạn)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        dataSource,
        npgsqlOptions => npgsqlOptions.UseVector()
    ));

var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:Key"];
var options = new Supabase.SupabaseOptions { AutoConnectRealtime = true };

// Đăng ký Supabase Client
builder.Services.AddScoped<Supabase.Client>(_ => new Supabase.Client(supabaseUrl, supabaseKey, options));

// Đăng ký Repositories nhoa
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Đăng ký Services nhoa
builder.Services.AddScoped<IAuthService, AuthService>();

// ================= SERVICES =================

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();

builder.Services.AddHttpContextAccessor();

// ================= CORS =================

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

var app = builder.Build();

// ================= PIPELINE =================

app.UseSwagger();

app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();