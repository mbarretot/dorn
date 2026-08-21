var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<ThemeInterop>();
builder.Services.AddScoped<ThemeState>();

builder.Services.AddScoped<ModalInterop>();
builder.Services.AddScoped<DismissInterop>();
builder.Services.AddScoped<AnchorInterop>();
builder.Services.AddScoped<ClipboardInterop>();

await builder.Build().RunAsync();
