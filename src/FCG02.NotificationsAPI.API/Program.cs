using FCG02.NotificationsAPI.API.Models;
using FCG02.NotificationsAPI.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

// injeção do serviço de envio de email simulado
builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();

// registro do worker consumer em segundo plano
builder.Services.AddHostedService<RabbitMqConsumerService>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "Health", service = "NotificationsAPI" }));

app.Run();

