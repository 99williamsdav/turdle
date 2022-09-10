using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Turdle;
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
    });
//builder.Services.AddSingleton<GameHubService>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<WordService>();
builder.Services.AddSingleton<IPointService, PointService>();
builder.Services.AddSingleton<IWordAnalysisService, WordAnalysisService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

var x = new WordService();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseHttpsRedirection();
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

var s1 = "hello";
var v1 = s1.GetHashCode();
var h1 = v1.ToString("x");
var s2 = "goodbye";
var v2 = s2.GetHashCode();
var h2 = v2.ToString("x");

app.Run();