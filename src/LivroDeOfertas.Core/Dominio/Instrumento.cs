namespace LivroDeOfertas.Core.Dominio;

/// <summary>
/// Ativo negociável: define o passo mínimo de preço (tick) e o lote mínimo de quantidade.
/// Preços e quantidades fora da grade são rejeitados antes de chegar ao livro.
/// </summary>
public sealed record Instrumento(string Codigo, decimal TickPreco, decimal LoteMinimo, string Moeda = "BRL")
{
    public string Codigo { get; } = ValidarCodigo(Codigo);
    public decimal TickPreco { get; } = TickPreco > 0 ? TickPreco : throw new ArgumentOutOfRangeException(nameof(TickPreco), "tick deve ser positivo");
    public decimal LoteMinimo { get; } = LoteMinimo > 0 ? LoteMinimo : throw new ArgumentOutOfRangeException(nameof(LoteMinimo), "lote deve ser positivo");

    /// <summary>Preço é múltiplo do tick e positivo.</summary>
    public bool PrecoValido(decimal preco) => preco > 0 && decimal.Remainder(preco, TickPreco) == 0;

    /// <summary>Quantidade é múltiplo do lote e positiva.</summary>
    public bool QuantidadeValida(decimal quantidade) => quantidade > 0 && decimal.Remainder(quantidade, LoteMinimo) == 0;

    /// <summary>Arredonda um preço para o tick mais próximo (útil para simuladores e conversões).</summary>
    public decimal ArredondarPreco(decimal preco) => Math.Round(preco / TickPreco, MidpointRounding.AwayFromZero) * TickPreco;

    private static string ValidarCodigo(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 12 || codigo.Any(c => !char.IsLetterOrDigit(c)))
            throw new ArgumentException("código do instrumento deve ter até 12 letras ou dígitos", nameof(codigo));
        return codigo.ToUpperInvariant();
    }
}
