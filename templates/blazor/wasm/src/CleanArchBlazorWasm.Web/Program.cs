var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<ThemeInterop>();
builder.Services.AddScoped<ThemeState>();

await builder.Build().RunAsync();
