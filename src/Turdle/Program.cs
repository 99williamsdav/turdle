using System.Configuration;
using System.Text.Json.Serialization;
using ChatGpt;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Turdle;
using Turdle.Bots;
using Turdle.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddLog4Net();

// Add services to the container.

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", p => p
        .WithOrigins("https://localhost:44419")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        var enumConverter = new JsonStringEnumConverter();
        options.JsonSerializerOptions.Converters.Add(enumConverter);
    });
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters
            .Add(new JsonStringEnumConverter());
    }).AddHubOptions<GameHub>(options =>
    {
        options.EnableDetailedErrors = true;
    }).AddHubOptions<AdminHub>(options =>
    {
        options.EnableDetailedErrors = true;
    }).AddHubOptions<HomeHub>(options =>
    {
        options.EnableDetailedErrors = true;
    });
//builder.Services.AddSingleton<GameHubService>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<WordService>();
builder.Services.AddSingleton<ChatGptClient>();
builder.Services.AddSingleton<ImageGenerationClient>();
builder.Services.AddSingleton<PersonalityAvatarService>();
builder.Services.AddSingleton<BotFactory>();
builder.Services.AddSingleton<IPointService, PointService>();
builder.Services.AddSingleton<IWordAnalysisService, WordAnalysisService>();

// config
builder.Services.Configure<ChatGptSettings>(builder.Configuration.GetSection("ChatGpt"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("CorsPolicy");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");
app.MapHub<GameHub>("/gameHub");
app.MapHub<AdminHub>("/adminHub");
app.MapHub<HomeHub>("/homeHub");

app.MapFallbackToFile("index.html");

app.Run();