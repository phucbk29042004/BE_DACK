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
using PayOS;

var builder = WebApplication.CreateBuilder(args);

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");


var connectionString = builder.Configuration.GetConnectionString("Connection");
builder.Services.AddDbContext<DACKContext>(options =>
    options.UseNpgsql(connectionString));


var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"] ?? "default_secret_key_at_least_32_chars_long";
var key = Encoding.UTF8.GetBytes(jwtKey);

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

// === 4. CẤU HÌNH CLOUDINARY ===
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));
builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CloudinarySettings>>().Value;
    var account = new Account(config.CloudName, config.ApiKey, config.ApiSecret);
    return new Cloudinary(account);
});
builder.Services.AddScoped<ICloudinaryService, Cloud>();

// === 5. CẤU HÌNH VNPAY ===
builder.Services.Configure<VNPaySettings>(builder.Configuration.GetSection("VNPay"));
builder.Services.AddSingleton<IVnpay, Vnpay>();

// === 5.1 CẤU HÌNH PAYOS ===
builder.Services.AddSingleton(provider =>
{
    var config = builder.Configuration.GetSection("PayOS");
    return new PayOSClient(
        config["ClientId"] ?? "",
        config["ApiKey"] ?? "",
        config["ChecksumKey"] ?? ""
    );
});

// === 6. CẤU HÌNH CORS (CHO PHÉP FRONTEND GỌI API) ===
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// === 7. CẤU HÌNH SWAGGER ===
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BE_DACK API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header sử dụng Bearer. Ví dụ: Bearer {token}",
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
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// === 8. MIDDLEWARE PIPELINE ===

app.UseExceptionHandler(c => c.Run(async context =>
{
    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/json";
    var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    await context.Response.WriteAsJsonAsync(new { success = false, message = "Internal Server Error", details = ex?.Message });
}));

// Luôn bật Swagger ở cả Production để bạn dễ test trên Render
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BE_DACK API v1");
    c.RoutePrefix = string.Empty; // Truy cập link chính sẽ ra luôn Swagger
});

app.UseRouting();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();