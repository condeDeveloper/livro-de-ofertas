namespace LivroDeOfertas.Core.Dominio;

/// <summary>Foto imutável de uma ordem em um instante. É o que os eventos carregam, para que o histórico não mude depois.</summary>
public sealed record FotoDaOrdem(
    long Id,
    string IdCliente,
    string Conta,
    string Instrumento,
    Lado Lado,
    TipoOrdem Tipo,
    Validade Validade,
    decimal? Preco,
    decimal Quantidade,
    decimal QuantidadeExecutada,
    SituacaoOrdem Situacao,
    DateTimeOffset CriadaEm,
    long Sequencia,
    string? MotivoEncerramento)
{
    public decimal QuantidadeRestante => Quantidade - QuantidadeExecutada;
}

/// <summary>Tudo que acontece no motor é publicado como evento, em ordem, para consumidores e para reconstrução.</summary>
public abstract record Evento(long Sequencia, DateTimeOffset Momento, string Instrumento);

public sealed record OrdemAceita(long Sequencia, DateTimeOffset Momento, FotoDaOrdem Ordem) : Evento(Sequencia, Momento, Ordem.Instrumento);

public sealed record OrdemRejeitada(long Sequencia, DateTimeOffset Momento, FotoDaOrdem Ordem, string Motivo) : Evento(Sequencia, Momento, Ordem.Instrumento);

public sealed record NegocioExecutado(long Sequencia, DateTimeOffset Momento, Negocio Negocio) : Evento(Sequencia, Momento, Negocio.Instrumento);

public sealed record OrdemCancelada(long Sequencia, DateTimeOffset Momento, FotoDaOrdem Ordem, string Motivo) : Evento(Sequencia, Momento, Ordem.Instrumento);

/// <summary>Alteração de preço e/ou quantidade. Guarda a foto depois da alteração e os valores anteriores.</summary>
public sealed record OrdemAlterada(long Sequencia, DateTimeOffset Momento, FotoDaOrdem Ordem, decimal? PrecoAnterior, decimal QuantidadeAnterior, bool PerdeuPrioridade)
    : Evento(Sequencia, Momento, Ordem.Instrumento);
