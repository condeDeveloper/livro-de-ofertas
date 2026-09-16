namespace LivroDeOfertas.Core.Dominio;

/// <summary>
/// Ordem enviada ao motor. O estado muda apenas por métodos internos do motor
/// (execução, cancelamento, alteração), para manter as invariantes.
/// </summary>
public sealed class Ordem
{
    public long Id { get; }
    public string IdCliente { get; }
    public string Conta { get; }
    public string Instrumento { get; }
    public Lado Lado { get; }
    public TipoOrdem Tipo { get; }
    public Validade Validade { get; }

    /// <summary>Preço limite; nulo para ordens a mercado.</summary>
    public decimal? Preco { get; private set; }

    public decimal Quantidade { get; private set; }
    public decimal QuantidadeExecutada { get; private set; }
    public decimal QuantidadeRestante => Quantidade - QuantidadeExecutada;
    public SituacaoOrdem Situacao { get; private set; } = SituacaoOrdem.Nova;
    public DateTimeOffset CriadaEm { get; }

    /// <summary>Sequência que define a prioridade temporal dentro de um nível de preço. Alterações que perdem prioridade recebem uma nova sequência.</summary>
    public long Sequencia { get; private set; }

    public string? MotivoEncerramento { get; private set; }

    internal Ordem(long id, string idCliente, string conta, string instrumento, Lado lado, TipoOrdem tipo, Validade validade,
        decimal? preco, decimal quantidade, DateTimeOffset criadaEm, long sequencia)
    {
        if (string.IsNullOrWhiteSpace(conta)) throw new ArgumentException("conta obrigatória", nameof(conta));
        if (quantidade <= 0) throw new ArgumentOutOfRangeException(nameof(quantidade), "quantidade deve ser positiva");
        if (tipo == TipoOrdem.Limitada && (preco is null || preco <= 0)) throw new ArgumentException("ordem limitada exige preço positivo", nameof(preco));
        if (tipo == TipoOrdem.Mercado && preco is not null) throw new ArgumentException("ordem a mercado não tem preço", nameof(preco));
        if (tipo == TipoOrdem.Mercado && validade == Validade.AteCancelar) throw new ArgumentException("ordem a mercado não pode ficar no livro; use IOC ou FOK", nameof(validade));

        Id = id;
        IdCliente = string.IsNullOrWhiteSpace(idCliente) ? id.ToString() : idCliente;
        Conta = conta;
        Instrumento = instrumento;
        Lado = lado;
        Tipo = tipo;
        Validade = validade;
        Preco = preco;
        Quantidade = quantidade;
        CriadaEm = criadaEm;
        Sequencia = sequencia;
    }

    public bool EstaAtiva => Situacao is SituacaoOrdem.Nova or SituacaoOrdem.ParcialmenteExecutada;

    /// <summary>Preço agressivo o suficiente para cruzar com o nível informado do lado oposto.</summary>
    public bool Cruza(decimal precoOposto) => Tipo == TipoOrdem.Mercado ||
        (Lado == Lado.Compra ? Preco!.Value >= precoOposto : Preco!.Value <= precoOposto);

    internal void Executar(decimal quantidade)
    {
        if (quantidade <= 0 || quantidade > QuantidadeRestante) throw new InvalidOperationException("quantidade executada inválida");
        QuantidadeExecutada += quantidade;
        Situacao = QuantidadeRestante == 0 ? SituacaoOrdem.Executada : SituacaoOrdem.ParcialmenteExecutada;
    }

    internal void Cancelar(string motivo)
    {
        if (!EstaAtiva) throw new InvalidOperationException("ordem não está ativa");
        Situacao = SituacaoOrdem.Cancelada;
        MotivoEncerramento = motivo;
    }

    internal void Rejeitar(string motivo)
    {
        Situacao = SituacaoOrdem.Rejeitada;
        MotivoEncerramento = motivo;
    }

    /// <summary>
    /// Altera preço e/ou quantidade. Reduzir quantidade mantém a prioridade; mudar preço ou aumentar
    /// quantidade manda a ordem para o fim da fila (nova sequência), como nas bolsas.
    /// </summary>
    internal bool Alterar(decimal? novoPreco, decimal? novaQuantidade, long novaSequencia)
    {
        if (!EstaAtiva) throw new InvalidOperationException("ordem não está ativa");
        var perdePrioridade = false;
        if (novoPreco is not null && novoPreco != Preco)
        {
            if (Tipo == TipoOrdem.Mercado) throw new InvalidOperationException("ordem a mercado não tem preço");
            Preco = novoPreco;
            perdePrioridade = true;
        }
        if (novaQuantidade is not null && novaQuantidade != Quantidade)
        {
            if (novaQuantidade <= QuantidadeExecutada) throw new InvalidOperationException("nova quantidade deve ser maior que a já executada");
            if (novaQuantidade > Quantidade) perdePrioridade = true;
            Quantidade = novaQuantidade.Value;
        }
        if (perdePrioridade) Sequencia = novaSequencia;
        return perdePrioridade;
    }

    /// <summary>Foto imutável do estado atual.</summary>
    public FotoDaOrdem Foto() => new(Id, IdCliente, Conta, Instrumento, Lado, Tipo, Validade, Preco, Quantidade, QuantidadeExecutada, Situacao, CriadaEm, Sequencia, MotivoEncerramento);

    public override string ToString() => $"#{Id} {Lado} {QuantidadeRestante}/{Quantidade} {Instrumento} @ {(Preco is null ? "MKT" : Preco.Value.ToString("0.00"))} [{Situacao}]";
}
