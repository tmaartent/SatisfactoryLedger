using System.Text.Json;
using System.Text.Json.Serialization;

using SatisfactoryLedger.Catalog;
using SatisfactoryLedger.World;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddCatalogModule();
builder.AddWorldModule();

// Errors are RFC 9457 ProblemDetails everywhere (chapter 05, "Status codes and errors").
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Enums go over the wire as strings ("solid"), not numbers (chapter 05, "Representation").
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    // ASP.NET's defaults also accept numbers written as strings ("5"), so OpenAPI documents every number as
    // number-or-string. Strict keeps numbers plain numbers in the contract and the generated client.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

var app = builder.Build();

// In Development, minimal APIs throw BadHttpRequestException for requests they can't bind (so you can debug them).
// Without a selector the exception handler turns those into 500; they are the client's mistake, so keep their 4xx.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError
});
app.UseStatusCodePages();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.Services.MigrateCatalogDatabaseAsync();
    await app.Services.MigrateWorldDatabaseAsync();
}

app.UseHttpsRedirection();

app.MapCatalogEndpoints();
app.MapWorldEndpoints();

await app.RunAsync();