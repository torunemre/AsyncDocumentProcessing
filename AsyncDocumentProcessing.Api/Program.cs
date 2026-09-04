
using AsyncDocumentProcessing.Application.Interfaces;
using AsyncDocumentProcessing.Application.Options;
using AsyncDocumentProcessing.Application.Services;
using AsyncDocumentProcessing.Infrastructure.DependencyInjection;
using AsyncDocumentProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AsyncDocumentProcessing.Api.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using AsyncDocumentProcessing.Application.Validators;

using System;
using Serilog;

var builder = WebApplication.CreateBuilder(args);


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        "Logs/app-.log",
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<UploadDocumentRequestValidator>();



builder.Services.Configure<DocumentProcessingOptions>(
    builder.Configuration.GetSection("DocumentProcessing"));

builder.Services.AddScoped<IDocumentService, DocumentService>();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseSqlServer(
//        builder.Configuration.GetConnectionString(
//            "DefaultConnection")));

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("DefaultConnection")!,
    Path.Combine(
        builder.Environment.ContentRootPath,
        "Storage"));

var app = builder.Build();


app.UseMiddleware<GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}