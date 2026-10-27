using System.Text.Json;
using System.Text.Json.Serialization;
using AurSil.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter()));
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("prices", c =>
{
    c.Timeout = TimeSpan.FromSeconds(20);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 AurSil/1.0");
});
builder.Services.AddHttpClient("razorpay", c => c.Timeout = TimeSpan.FromSeconds(20));

builder.Services.AddSingleton(builder.Configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection("Pricing").Get<PricingOptions>() ?? new PricingOptions());
builder.Services.AddSingleton(builder.Configuration.GetSection("Razorpay").Get<RazorpayOptions>() ?? new RazorpayOptions());
builder.Services.AddSingleton<EmailSender>();
builder.Services.AddSingleton<OtpService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<PriceService>();
builder.Services.AddSingleton<RazorpayService>();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddHostedService<PriceUpdater>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = TokenService.SigningKey(builder.Configuration),
        ClockSkew = TimeSpan.FromMinutes(1),
    });
builder.Services.AddAuthorization();

// Lets the browser preview (preview/aursil.html) call the API during development
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { app = "AurSil API", status = "running" }));
app.MapControllers();

app.Run();

// SQL Server returns DateTime without a kind; everything is stored in UTC, so say so in the JSON
class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ"));
}
