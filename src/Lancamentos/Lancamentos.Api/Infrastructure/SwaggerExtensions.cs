using Lancamentos.Api.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Lancamentos.Api.Infrastructure;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Fluxo de Caixa — Lançamentos",
                Version = "v1",
                Description = "Registro de créditos, débitos e estornos do fluxo de caixa do lojista.",
            });

            var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
            if (File.Exists(xml))
            {
                options.IncludeXmlComments(xml);
            }

            options.OperationFilter<IdempotencyKeyHeaderFilter>();
        });

        return services;
    }

    public static WebApplication UseSwaggerIfEnabled(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            return app;
        }

        app.UseSwagger();
        app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Lançamentos v1"));
        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
        return app;
    }

    private sealed class IdempotencyKeyHeaderFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var requiresKey = context.ApiDescription.ActionDescriptor.FilterDescriptors
                .Any(f => f.Filter is ServiceFilterAttribute { ServiceType: { } t } && t == typeof(IdempotencyFilter));
            if (!requiresKey)
            {
                return;
            }

            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = IdempotencyFilter.HeaderName,
                In = ParameterLocation.Header,
                Required = true,
                Description = "Chave única da operação (ex.: um GUID). Repetir a chamada com a mesma chave não duplica o lançamento.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 100 },
            });
        }
    }
}
