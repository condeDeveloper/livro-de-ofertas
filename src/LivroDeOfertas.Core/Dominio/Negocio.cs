namespace LivroDeOfertas.Core.Dominio;

/// <summary>Execução entre uma ordem de compra e uma de venda. O preço é sempre o da ordem que estava no livro.</summary>
public sealed record Negocio(
    long Id,
    string Instrumento,
    decimal Preco,
    decimal Quantidade,
    long OrdemCompraId,
    long OrdemVendaId,
    string ContaCompra,
    string ContaVenda,
    Lado Agressor,
    DateTimeOffset Momento)
{
    public decimal Volume => Preco * Quantidade;
}
