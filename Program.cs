using System.Reflection;
using System.Text.Json.Serialization;
using Coflnet.Core;
using Coflnet.Excel;
using Coflnet.Tab;
using DemoApi.Excel;
using Microsoft.OpenApi.Models;
using OpenAI.Extensions;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DemoApi",
        Version = "v1",
        Description = ""
    });
    AddBaererTokenAuth(c);
    AddFileUploadDoc(c);
    AddXmlDoc(c);
});
builder.Services.AddCoflnetCore();
builder.Services.AddSingleton<AIPromtService>();
builder.Services.AddSingleton<IIsCompanyService,IsCompanyService>();
builder.Services.AddSingleton<BrandMappingService>();
builder.Services.AddSingleton<SurveryGenerator>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddOpenAIService(settings => { settings.ApiKey = builder.Configuration["OpenAiApiKey"]; });
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        name: MyAllowSpecificOrigins,
        builder =>
        {
            builder
                .WithOrigins("*")
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
    );
});

var app = builder.Build();

app.UseSwagger(c =>
{
    c.RouteTemplate = "api/openapi/{documentName}/openapi.json";
})
.UseSwaggerUI(c =>
{
    c.RoutePrefix = "api";
    c.SwaggerEndpoint("/api/openapi/v1/openapi.json", "Ane");
    c.EnablePersistAuthorization();
    c.EnableTryItOutByDefault();

});

app.UseCoflnetCore();
// log every request
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Path}");
    await next();
});
app.UseCors(MyAllowSpecificOrigins);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static void AddXmlDoc(SwaggerGenOptions c)
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath, true);
}

static void AddFileUploadDoc(SwaggerGenOptions c)
{
    c.CustomOperationIds(apiDesc =>
    {
        return apiDesc.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : "xy";
    });
    c.OperationFilter<FileUploadOperationFilter>();
}

static void AddBaererTokenAuth(SwaggerGenOptions c)
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
}