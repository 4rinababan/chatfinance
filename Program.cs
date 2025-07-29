using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
    
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // app.UseSwagger();
    // app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseExceptionHandler("/error");

app.UseAuthorization();
app.MapControllers();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<AppDbContext>();

    var retries = 10;
    while (retries > 0)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex)
        {
            retries--;
            logger.LogWarning(ex, "Migration failed, retrying... attempts left: {Retries}", retries);
            Thread.Sleep(2000); // tunggu 2 detik
        }
    }

    if (retries == 0)
    {
        logger.LogError("Migration failed after all retries");
         throw new Exception("Migration failed after retries"); // ✅
    }
}

app.Map("/error", (HttpContext http) =>
{
    var feature = http.Features.Get<IExceptionHandlerFeature>();
    return Results.Problem(feature?.Error.Message);
});

// ✅ Tambahkan baris ini
app.Urls.Add("http://*:8080");

app.Run();
