//using Microsoft.EntityFrameworkCore;

//using ParentalShieldBE.Db;
//using ParentalShieldBE.Repositories;
//using ParentalShieldBE.Repositories.IRepositories;
//using ParentalShieldBE.Config;
//using ParentalShieldBE.Services;
//using ParentalShieldBE.Services.IServices;
//using Microsoft.OpenApi;
//using Microsoft.OpenApi.Models;

//namespace ParentalShieldBE.Extensions;

//public static class ServiceCollectionExtensions
//{
//    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
//    {
//        var connectionString = configuration.GetConnectionString("DefaultConnection");

//        services.AddDbContext<AppDbContext>(options =>
//            options.UseNpgsql(connectionString, npgsqlOptions =>
//            {
//                npgsqlOptions.EnableRetryOnFailure(
//                    maxRetryCount: 5,
//                    maxRetryDelay: TimeSpan.FromSeconds(10),
//                    errorCodesToAdd: null
//                );
//                npgsqlOptions.CommandTimeout(60);
//            }));
//        return services;
//    }

//    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
//    {
//        var origins = configuration.GetSection("CORS:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:3000", "http://localhost:5173" };
//        services.AddCors(options =>
//        {
//            options.AddDefaultPolicy(policy =>
//            {
//                policy.WithOrigins(origins)
//                    .AllowAnyHeader()
//                    .AllowAnyMethod()
//                    .AllowCredentials();
//            });
//        });
//        return services;
//    }

//    public static IServiceCollection AddSwaggerConfig(this IServiceCollection services)
//    {
//        services.AddEndpointsApiExplorer();
//        services.AddSwaggerGen(options =>
//        {
//            options.SwaggerDoc("v1", new OpenApiInfo
//            {
//                Title = "Edugard API",
//                Version = "v1",
//                Description = "Backend API for ED application."
//            });

//            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
//            {
//                Name = "Authorization",
//                Type = SecuritySchemeType.Http,
//                Scheme = "Bearer",
//                In = ParameterLocation.Header,
//                Description = "JWT Authorization header. Example: \"Bearer {token}\""
//            });
//            options.AddSecurityRequirement(new OpenApiSecurityRequirement
//            {
//                {
//                    new OpenApiSecurityScheme
//                    {
//                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
//                    },
//                    Array.Empty<string>()
//                }
//            });

//        });
//        return services;
//    }

//    /// <summary>
//    /// Đăng ký các service/repository dùng trong ứng dụng.
//    /// Thêm vào đây khi tạo IUserService, UserService, v.v.
//    /// </summary>
//    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
//    {
     
      
//        return services;
//    }
//}
