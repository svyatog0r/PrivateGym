using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PrivateGym.Api.Data;
using PrivateGym.Api.Models;
using PrivateGym.Api.Services;
using System.Text;
using System.Text.Json;

namespace PrivateGym.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                });

            builder.Services.AddOpenApi();
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                        ValidAudience = builder.Configuration["JwtSettings:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]!))
                    };
                });

            builder.Services.AddAuthorization();
            builder.Services.AddHostedService<BookingStatusUpdateService>();
            var app = builder.Build();

            if (app.Environment.IsDevelopment()) app.MapOpenApi();

            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseCors("AllowAll");

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.MapGet("/", (HttpContext context) =>
            {
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    return Results.Redirect("/index.html");
                }
                return Results.Redirect("/landing.html");
            });

            // Применяем миграции автоматически при старте приложения.
            // В Docker контейнер стартует - схема БД подтягивается сама.
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();

                // создаём админа
                if (!db.Users.Any(u => u.Email == "admin@privategym.local"))
                {
                    db.Users.Add(new User
                    {
                        FullName = "Администратор",
                        Email = "admin@privategym.local",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                        Role = UserRole.Admin
                    });
                    db.SaveChanges();
                }
            }

            app.Run();
        }
    }
}