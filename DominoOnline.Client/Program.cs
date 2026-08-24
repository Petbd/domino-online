using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DominoOnline.Client;
using DominoOnline.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Регистрируем наш сервис как Singleton (один на всё приложение)
builder.Services.AddSingleton<GameHubService>();

await builder.Build().RunAsync();
