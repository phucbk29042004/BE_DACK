using BE_DACK.Models.Entities;
using BE_DACK.Models.Model;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuanLyDatVeMayBay.Services.VnpayServices;
using System.Text;
using WebAppDoCongNghe.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DACKContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Connection")));

var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

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
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

builder.Services.Configure<CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<CloudinarySettings>>().Value;
    var account = new Account(config.CloudName, config.ApiKey, config.ApiSecret);
    return new Cloudinary(account);
});

builder.Services.AddScoped<ICloudinaryService, Cloud>();
builder.Services.Configure<VNPaySettings>(builder.Configuration.GetSection("VNPay"));
builder.Services.AddSingleton<IVnpay, Vnpay>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BE_DACK API",
        Version = "v1",
        Description = "API DACK"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = @"JWT Authorization header sử dụng Bearer.  
                        Nhập token theo định dạng sau: **Bearer [token]**",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// CORS phải được đặt trước các middleware khác
app.UseCors("AllowAll");

// Routing và Authentication
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Swagger phải được đặt sau MapControllers
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BE_DACK API v1");
    c.RoutePrefix = "swagger";
});

app.Run();