using AccountService;
using ServiceBusService;
using Swashbuckle.AspNetCore.SwaggerGen; // Optional, but sometimes needed
using Swashbuckle.AspNetCore.SwaggerUI; // Add this using directive

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Services
builder.Services.AddSingleton<BankServiceBusService>(provider =>
    new BankServiceBusService(
        builder.Configuration.GetConnectionString("AzureServiceBus"),
        provider.GetRequiredService<ILogger<BankServiceBusService>>()
    ));

builder.Services.AddScoped<BankAccountService>(provider =>
    new BankAccountService(
        provider.GetRequiredService<BankServiceBusService>(),
        builder.Configuration.GetConnectionString("BankDatabase"),
        provider.GetRequiredService<ILogger<BankAccountService>>()
    ));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
