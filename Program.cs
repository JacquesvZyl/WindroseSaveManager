using WindroseSaveManager;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.Configure<WindroseOptions>(builder.Configuration.GetSection("Windrose"));
builder.Services.AddSingleton<CommandRunner>();
builder.Services.AddSingleton<WindroseWorldService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
