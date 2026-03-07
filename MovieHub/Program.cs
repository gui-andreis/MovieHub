using Microsoft.EntityFrameworkCore;
using MovieHub.Data;
using MovieHub.Extensions;

var builder = WebApplication.CreateBuilder(args);
// Registra os controladores da API e habilita suporte a endpoints
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Registra configurações modulares da aplicação (separadas via Extensions)
builder.Services.AddSwaggerConfiguration();
builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddRateLimiterConfiguration();

var app = builder.Build();

// Executa migrations automaticamente (apenas para ambiente de desenvolvimento)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//Garante que as Roles padrão (ex: Admin, User) e o usuário administrador inicial existam
await app.SeedRolesAndAdminAsync();

app.UseRateLimiter();
app.UseAppMiddlewares();
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("fixed");
app.Run();