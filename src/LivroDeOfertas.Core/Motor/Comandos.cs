using LivroDeOfertas.Core.Dominio;

namespace LivroDeOfertas.Core.Motor;

/// <summary>Pedido de nova ordem, antes de virar uma <see cref="Ordem"/> com identidade.</summary>
public sealed record NovaOrdem(
    string Instrumento,
    string Conta,
    Lado Lado,
    TipoOrdem Tipo,
    decimal Quantidade,
    decimal? Preco = null,
    Validade Validade = Validade.AteCancelar,
    string? IdCliente = null);

public sealed record AlterarOrdem(long OrdemId, string Conta, decimal? NovoPreco = null, decimal? NovaQuantidade = null);

public sealed record CancelarOrdem(long OrdemId, string Conta);

/// <summary>Resultado de um comando: a ordem afetada e os eventos gerados, na ordem em que ocorreram.</summary>
public sealed record Resultado(Ordem? Ordem, IReadOnlyList<Evento> Eventos)
{
    public bool Aceita => Ordem is not null && Ordem.Situacao != SituacaoOrdem.Rejeitada;
    public IEnumerable<Negocio> Negocios => Eventos.OfType<NegocioExecutado>().Select(e => e.Negocio);
    public string? Motivo => Eventos.OfType<OrdemRejeitada>().FirstOrDefault()?.Motivo ?? Eventos.OfType<OrdemCancelada>().LastOrDefault()?.Motivo;
}
