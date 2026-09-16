using System.ComponentModel.DataAnnotations;
using LivroDeOfertas.Core.Dados;
using LivroDeOfertas.Core.Dominio;

namespace LivroDeOfertas.Api.Contratos;

public sealed record NovaOrdemRequest(
    [Required] string Instrumento,
    [Required] string Conta,
    Lado Lado,
    TipoOrdem Tipo,
    [Range(typeof(decimal), "0.000001", "1000000000")] decimal Quantidade,
    decimal? Preco,
    Validade Validade = Validade.AteCancelar,
    string? IdCliente = null);

public sealed record AlterarOrdemRequest([Required] string Conta, decimal? NovoPreco, decimal? NovaQuantidade);

public sealed record CancelarOrdemRequest([Required] string Conta);

public sealed record OrdemResponse(
    long Id, string IdCliente, string Conta, string Instrumento, Lado Lado, TipoOrdem Tipo, Validade Validade,
    decimal? Preco, decimal Quantidade, decimal QuantidadeExecutada, decimal QuantidadeRestante, SituacaoOrdem Situacao,
    DateTimeOffset CriadaEm, string? Motivo)
{
    public static OrdemResponse De(FotoDaOrdem o) => new(o.Id, o.IdCliente, o.Conta, o.Instrumento, o.Lado, o.Tipo, o.Validade,
        o.Preco, o.Quantidade, o.QuantidadeExecutada, o.QuantidadeRestante, o.Situacao, o.CriadaEm, o.MotivoEncerramento);
}

public sealed record NegocioResponse(long Id, string Instrumento, decimal Preco, decimal Quantidade, decimal Volume, Lado Agressor, DateTimeOffset Momento,
    long OrdemCompraId, long OrdemVendaId)
{
    public static NegocioResponse De(Negocio n) => new(n.Id, n.Instrumento, n.Preco, n.Quantidade, n.Volume, n.Agressor, n.Momento, n.OrdemCompraId, n.OrdemVendaId);
}

public sealed record ResultadoResponse(OrdemResponse Ordem, IReadOnlyList<NegocioResponse> Negocios, IReadOnlyList<string> Eventos);

public sealed record CandleResponse(DateTimeOffset Inicio, DateTimeOffset Fim, decimal Abertura, decimal Maxima, decimal Minima, decimal Fechamento,
    decimal Quantidade, decimal Volume, int Negocios, decimal Vwap)
{
    public static CandleResponse De(Candle c) => new(c.Inicio, c.Fim, c.Abertura, c.Maxima, c.Minima, c.Fechamento, c.Quantidade, c.Volume, c.Negocios, c.Vwap);
}

public sealed record EstatisticasResponse(string Instrumento, decimal? Ultimo, decimal? Abertura, decimal? Maxima, decimal? Minima, decimal Quantidade,
    decimal Volume, int Negocios, decimal? Vwap, decimal? VariacaoPercentual, DateTimeOffset? UltimoNegocioEm)
{
    public static EstatisticasResponse De(Estatisticas e) => new(e.Instrumento, e.Ultimo, e.Abertura, e.Maxima, e.Minima, e.Quantidade, e.Volume, e.Negocios, e.Vwap, e.Variacao, e.UltimoNegocioEm);
}

public sealed record InstrumentoResponse(string Codigo, decimal TickPreco, decimal LoteMinimo, string Moeda)
{
    public static InstrumentoResponse De(Instrumento i) => new(i.Codigo, i.TickPreco, i.LoteMinimo, i.Moeda);
}
