namespace LivroDeOfertas.Core.Dominio;

/// <summary>
/// Livro de um instrumento: compras ordenadas do maior para o menor preço, vendas do menor para o maior,
/// cada nível com fila por chegada. Só o motor altera o livro.
/// </summary>
public sealed class Livro
{
    private readonly SortedDictionary<decimal, NivelPreco> _compras = new(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));
    private readonly SortedDictionary<decimal, NivelPreco> _vendas = new();
    private readonly Dictionary<long, Ordem> _ordens = new();

    public Livro(Instrumento instrumento) => Instrumento = instrumento;

    public Instrumento Instrumento { get; }

    public NivelPreco? MelhorCompra => _compras.Count == 0 ? null : _compras.First().Value;
    public NivelPreco? MelhorVenda => _vendas.Count == 0 ? null : _vendas.First().Value;

    public decimal? Spread => MelhorCompra is null || MelhorVenda is null ? null : MelhorVenda.Preco - MelhorCompra.Preco;
    public decimal? PrecoMedio => MelhorCompra is null || MelhorVenda is null ? null : (MelhorVenda.Preco + MelhorCompra.Preco) / 2;

    public int OrdensAtivas => _ordens.Count;

    public IEnumerable<NivelPreco> Compras => _compras.Values;
    public IEnumerable<NivelPreco> Vendas => _vendas.Values;

    public Ordem? Obter(long id) => _ordens.GetValueOrDefault(id);

    /// <summary>Livro está cruzado se a melhor compra for maior ou igual à melhor venda: nunca deve acontecer após o casamento.</summary>
    public bool Cruzado => MelhorCompra is not null && MelhorVenda is not null && MelhorCompra.Preco >= MelhorVenda.Preco;

    internal void Adicionar(Ordem ordem)
    {
        if (ordem.Tipo == TipoOrdem.Mercado) throw new InvalidOperationException("ordem a mercado não entra no livro");
        var lado = Lado(ordem.Lado);
        if (!lado.TryGetValue(ordem.Preco!.Value, out var nivel))
        {
            nivel = new NivelPreco(ordem.Preco.Value);
            lado[ordem.Preco.Value] = nivel;
        }
        nivel.Adicionar(ordem);
        _ordens[ordem.Id] = ordem;
    }

    internal bool Remover(Ordem ordem)
    {
        if (!_ordens.Remove(ordem.Id)) return false;
        var lado = Lado(ordem.Lado);
        if (lado.TryGetValue(ordem.Preco!.Value, out var nivel))
        {
            nivel.Remover(ordem);
            if (nivel.Vazio) lado.Remove(nivel.Preco);
        }
        return true;
    }

    /// <summary>Ajusta a quantidade agregada do nível quando a ordem muda de quantidade sem sair da fila.</summary>
    internal void AjustarQuantidade(Ordem ordem, decimal delta)
    {
        if (Lado(ordem.Lado).TryGetValue(ordem.Preco!.Value, out var nivel)) nivel.Ajustar(delta);
    }

    /// <summary>Depois de uma execução parcial, desconta a quantidade do nível e remove o nível se esvaziou.</summary>
    internal void AposExecucao(Ordem ordem, decimal quantidade)
    {
        var lado = Lado(ordem.Lado);
        var nivel = lado[ordem.Preco!.Value];
        nivel.Ajustar(-quantidade);
        if (ordem.QuantidadeRestante == 0)
        {
            nivel.Remover(ordem); // restante já é zero: só tira da fila
            _ordens.Remove(ordem.Id);
            if (nivel.Vazio) lado.Remove(nivel.Preco);
        }
    }

    /// <summary>Quantidade disponível no lado oposto a preços que a ordem aceita. Usado pelo FOK.</summary>
    public decimal DisponivelPara(Ordem ordem)
    {
        var total = 0m;
        foreach (var nivel in Lado(ordem.Lado.Oposto()).Values)
        {
            if (!ordem.Cruza(nivel.Preco)) break;
            total += nivel.Quantidade;
            if (total >= ordem.QuantidadeRestante) break;
        }
        return total;
    }

    /// <summary>Foto dos N melhores níveis de cada lado.</summary>
    public FotoDoLivro Foto(int niveis = 10) => new(
        Instrumento.Codigo,
        _compras.Values.Take(niveis).Select(n => new NivelFoto(n.Preco, n.Quantidade, n.Ordens)).ToArray(),
        _vendas.Values.Take(niveis).Select(n => new NivelFoto(n.Preco, n.Quantidade, n.Ordens)).ToArray());

    private SortedDictionary<decimal, NivelPreco> Lado(Lado lado) => lado == Dominio.Lado.Compra ? _compras : _vendas;
}

public sealed record NivelFoto(decimal Preco, decimal Quantidade, int Ordens);

public sealed record FotoDoLivro(string Instrumento, IReadOnlyList<NivelFoto> Compras, IReadOnlyList<NivelFoto> Vendas)
{
    public decimal? MelhorCompra => Compras.Count == 0 ? null : Compras[0].Preco;
    public decimal? MelhorVenda => Vendas.Count == 0 ? null : Vendas[0].Preco;
    public decimal? Spread => MelhorCompra is null || MelhorVenda is null ? null : MelhorVenda - MelhorCompra;
}
