using LivroDeOfertas.Api.Contratos;
using LivroDeOfertas.Core.Motor;

namespace LivroDeOfertas.Api.Endpoints;

public static class MercadoEndpoints
{
    private static readonly Dictionary<string, TimeSpan> Intervalos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1s"] = TimeSpan.FromSeconds(1), ["5s"] = TimeSpan.FromSeconds(5), ["15s"] = TimeSpan.FromSeconds(15),
        ["1m"] = TimeSpan.FromMinutes(1), ["5m"] = TimeSpan.FromMinutes(5), ["15m"] = TimeSpan.FromMinutes(15),
        ["1h"] = TimeSpan.FromHours(1), ["1d"] = TimeSpan.FromDays(1),
    };

    public static IEndpointRouteBuilder MapMercado(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api").WithTags("Mercado");

        grupo.MapGet("/instrumentos", (Bolsa bolsa) => bolsa.Instrumentos.Select(InstrumentoResponse.De))
            .WithSummary("Instrumentos listados");

        grupo.MapGet("/livro/{instrumento}", (string instrumento, int? niveis, Bolsa bolsa) =>
            bolsa.Existe(instrumento) ? Results.Ok(bolsa.Foto(instrumento, Math.Clamp(niveis ?? 10, 1, 50))) : Results.NotFound())
            .WithSummary("Foto do livro: N melhores níveis de cada lado (?niveis=10)");

        grupo.MapGet("/negocios/{instrumento}", (string instrumento, int? ultimos, Bolsa bolsa) =>
            bolsa.Existe(instrumento) ? Results.Ok(bolsa.Negocios(instrumento, Math.Clamp(ultimos ?? 50, 1, 1000)).Select(NegocioResponse.De)) : Results.NotFound())
            .WithSummary("Últimos negócios (?ultimos=50)");

        grupo.MapGet("/candles/{instrumento}", (string instrumento, string? intervalo, int? ultimos, Bolsa bolsa) =>
        {
            if (!bolsa.Existe(instrumento)) return Results.NotFound();
            if (!Intervalos.TryGetValue(intervalo ?? "1m", out var ts)) return Results.BadRequest(new { title = "intervalo inválido; use " + string.Join(", ", Intervalos.Keys), status = 400 });
            return Results.Ok(bolsa.Candles(instrumento, ts, Math.Clamp(ultimos ?? 100, 1, 1440)).Select(CandleResponse.De));
        }).WithSummary("Candles OHLCV (?intervalo=1m&ultimos=100)");

        grupo.MapGet("/estatisticas/{instrumento}", (string instrumento, Bolsa bolsa) =>
            bolsa.Existe(instrumento) ? Results.Ok(EstatisticasResponse.De(bolsa.Estatisticas(instrumento))) : Results.NotFound())
            .WithSummary("Resumo do pregão: último, abertura, máxima, mínima, volume, VWAP, variação");

        grupo.MapGet("/eventos", (long? desde, int? limite, Bolsa bolsa) =>
            bolsa.Registro.Desde(desde ?? 0).Take(Math.Clamp(limite ?? 200, 1, 5000))
                .Select(e => new { e.Sequencia, e.Momento, e.Instrumento, Tipo = e.GetType().Name, Dados = (object)e }))
            .WithSummary("Registro de eventos append-only (?desde=sequencia&limite=200)");

        return app;
    }
}
