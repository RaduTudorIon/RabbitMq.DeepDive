using RabbitMq.DeepDive.ApiService.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

// Add controller support for RabbitMQ Management API
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();

// Register RabbitMQ Management API service
builder.Services.AddScoped<IRabbitMqApiService, RabbitMqApiService>();

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("RabbitMQ Management API")
            .WithTheme(ScalarTheme.Mars)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

// Redirect root to Scalar UI for RabbitMQ Management API
app.MapGet("/", () => Results.Redirect("/scalar/v1"))
    .ExcludeFromDescription();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();