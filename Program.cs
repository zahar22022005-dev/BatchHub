var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(...);
builder.Services.AddHttpClient();

var app = builder.Build();

// ТЕСТОВЫЙ ЭНДПОИНТ
app.MapGet("/ping", () => "pong");

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
