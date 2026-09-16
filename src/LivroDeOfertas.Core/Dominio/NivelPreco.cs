namespace LivroDeOfertas.Core.Dominio;

/// <summary>Fila FIFO de ordens em um mesmo preço. Mantém a quantidade agregada para consultas O(1).</summary>
public sealed class NivelPreco
{
    private readonly LinkedList<Ordem> _fila = new();
    private readonly Dictionary<long, LinkedListNode<Ordem>> _indice = new();

    public NivelPreco(decimal preco) => Preco = preco;

    public decimal Preco { get; }
    public decimal Quantidade { get; private set; }
    public int Ordens => _fila.Count;
    public bool Vazio => _fila.Count == 0;

    public Ordem? Primeira => _fila.First?.Value;

    public IEnumerable<Ordem> EmOrdemDePrioridade => _fila;

    internal void Adicionar(Ordem ordem)
    {
        var no = _fila.AddLast(ordem);
        _indice[ordem.Id] = no;
        Quantidade += ordem.QuantidadeRestante;
    }

    internal bool Remover(Ordem ordem)
    {
        if (!_indice.Remove(ordem.Id, out var no)) return false;
        _fila.Remove(no);
        Quantidade -= ordem.QuantidadeRestante;
        return true;
    }

    /// <summary>Ajusta a quantidade agregada depois de uma execução parcial ou alteração da ordem.</summary>
    internal void Ajustar(decimal delta) => Quantidade += delta;

    public bool Contem(long ordemId) => _indice.ContainsKey(ordemId);
}
