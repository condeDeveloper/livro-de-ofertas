using LivroDeOfertas.Api.Contratos;
using LivroDeOfertas.Core.Dominio;
using LivroDeOfertas.Core.Motor;

namespace LivroDeOfertas.Api.Endpoints;

public static class OrdensEndpoints
{
    public static IEndpointRouteBuilder MapOrdens(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/ordens").WithTags("Ordens");

        grupo.MapPost("/", (NovaOrdemRequest req, Bolsa bolsa) =>
        {
            if (!bolsa.Existe(req.Instrumento)) return Results.NotFound(Problema("instrumento não listado", 404));
            var r = bolsa.Enviar(new NovaOrdem(req.Instrumento.ToUpperInvariant(), req.Conta, req.Lado, req.Tipo, req.Quantidade, req.Preco, req.Validade, req.IdCliente));
            var corpo = Converter(r);
            return r.Aceita ? Results.Created($"/api/ordens/{req.Instrumento.ToUpperInvariant()}/{r.Ordem!.Id}", corpo) : Results.UnprocessableEntity(corpo);
        })
        .WithSummary("Envia uma ordem")
        .WithDescription("Limitada ou a mercado, com validade GTC, IOC ou FOK. Devolve a ordem e os negócios gerados na hora.")
        .Produces<ResultadoResponse>(201).Produces<ResultadoResponse>(422);

        grupo.MapGet("/{instrumento}/{id:long}", (string instrumento, long id, Bolsa bolsa) =>
        {
            if (!bolsa.Existe(instrumento)) return Results.NotFound(Problema("instrumento não listado", 404));
            var o = bolsa.Ordem(instrumento, id);
            return o is null ? Results.NotFound(Problema("ordem não encontrada", 404)) : Results.Ok(OrdemResponse.De(o));
        }).WithSummary("Consulta uma ordem, ativa ou encerrada");

        grupo.MapGet("/{instrumento}/conta/{conta}", (string instrumento, string conta, bool? todas, Bolsa bolsa) =>
        {
            if (!bolsa.Existe(instrumento)) return Results.NotFound(Problema("instrumento não listado", 404));
            return Results.Ok(bolsa.OrdensDaConta(instrumento, conta, somenteAtivas: todas != true).Select(OrdemResponse.De));
        }).WithSummary("Ordens de uma conta no instrumento (ativas por padrão; ?todas=true inclui encerradas)");

        grupo.MapPut("/{instrumento}/{id:long}", (string instrumento, long id, AlterarOrdemRequest req, Bolsa bolsa) =>
        {
            if (!bolsa.Existe(instrumento)) return Results.NotFound(Problema("instrumento não listado", 404));
            if (req.NovoPreco is null && req.NovaQuantidade is null) return Results.BadRequest(Problema("informe novo preço e/ou nova quantidade", 400));
            var r = bolsa.Alterar(instrumento, new AlterarOrdem(id, req.Conta, req.NovoPreco, req.NovaQuantidade));
            return r.Ordem is null ? Results.NotFound(Problema("ordem não está ativa", 404)) : Results.Ok(Converter(r));
        })
        .WithSummary("Altera preço e/ou quantidade")
        .WithDescription("Reduzir quantidade mantém a prioridade; mudar preço ou aumentar quantidade manda a ordem para o fim da fila.");

        grupo.MapDelete("/{instrumento}/{id:long}", (string instrumento, long id, string conta, Bolsa bolsa) =>
        {
            if (!bolsa.Existe(instrumento)) return Results.NotFound(Problema("instrumento não listado", 404));
            var r = bolsa.Cancelar(instrumento, new CancelarOrdem(id, conta));
            return r.Ordem is null ? Results.NotFound(Problema("ordem não está ativa", 404)) : Results.Ok(OrdemResponse.De(r.Ordem.Foto()));
        }).WithSummary("Cancela uma ordem ativa (?conta=)");

        return app;
    }

    internal static ResultadoResponse Converter(Resultado r) => new(
        OrdemResponse.De(r.Ordem!.Foto()),
        r.Negocios.Select(NegocioResponse.De).ToArray(),
        r.Eventos.Select(e => e.GetType().Name).ToArray());

    internal static object Problema(string detalhe, int status) => new { title = detalhe, status };
}
