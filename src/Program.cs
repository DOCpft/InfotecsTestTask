using CmdScale.EntityFrameworkCore.TimescaleDB;
using InfotecsTestTask.Abstract;
using InfotecsTestTask.DataAccess;
using InfotecsTestTask.Services;
using InfotecsTestTask.Services.ConcreteServices;
using InfotecsTestTask.Services.Configuration;
using InfotecsTestTask.Services.Factories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(s =>
{
    s.EnableAnnotations();
    s.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "InfotecsTestTask API",
        Version = "v1"
    });
});

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("InfotecsDB")).UseTimescaleDb());

builder.Services.AddSingleton<IFileProcessingConfiguration, FileProcessingConfigurationService>();

builder.Services.AddScoped<IFileProcessingService, CsvProcessingService>();

builder.Services.AddScoped<IFileProcessorFactory, FileProcessorFactory>();

builder.Services.AddScoped<UnivarsalFileProcessingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();


// Для интеграционных тестов
public partial class Program { }