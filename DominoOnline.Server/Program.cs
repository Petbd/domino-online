using DominoOnline.Server.Hubs;
using DominoOnline.Server.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Environment.WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

// Только для Render.com (переменная окружения PORT). 
// Локально используем порт из launchSettings.json (5000)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<GameService>();
builder.Services.AddSignalR();

var app = builder.Build();

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.MapHub<GameHub>("/gamehub");
app.MapGet("/health", () => "OK");
app.MapFallbackToFile("index.html");

app.Run();
