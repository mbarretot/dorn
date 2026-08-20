var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddScoped<ThemeInterop>();
builder.Services.AddScoped<ThemeState>();

builder.Services.AddScoped<ModalInterop>();
builder.Services.AddScoped<DismissInterop>();
builder.Services.AddScoped<AnchorInterop>();
builder.Services.AddScoped<ClipboardInterop>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
