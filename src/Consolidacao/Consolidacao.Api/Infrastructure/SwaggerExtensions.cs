using Microsoft.OpenApi;

namespace Consolidacao.Api.Infrastructure;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Fluxo de Caixa — Consolidação",
                Version = "v1",
                Description = "Saldo diário consolidado, alimentado de forma assíncrona pelos lançamentos.",
            });

            var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
            if (File.Exists(xml))
            {
                options.IncludeXmlComments(xml);
            }
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
        app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Consolidação v1"));
        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
        return app;
    }
}
