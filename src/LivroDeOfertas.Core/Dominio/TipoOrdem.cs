namespace LivroDeOfertas.Core.Dominio;

/// <summary>Tipo da ordem.</summary>
public enum TipoOrdem
{
    /// <summary>Executa somente ao preço informado ou melhor; o que sobrar fica no livro.</summary>
    Limitada,

    /// <summary>Executa contra o que houver no livro, varrendo níveis; nunca fica registrada.</summary>
    Mercado,
}
