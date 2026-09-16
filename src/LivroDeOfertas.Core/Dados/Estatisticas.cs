using LivroDeOfertas.Core.Dominio;

namespace LivroDeOfertas.Core.Dados;

/// <summary>Resumo do pregão de um instrumento: último, abertura, máxima, mínima, volume e VWAP.</summary>
public sealed class Estatisticas
{
    public Estatisticas(string instrumento) => Instrumento = instrumento;

    public string Instrumento { get; }
    public decimal? Ultimo { get; private set; }
    public decimal? Abertura { get; private set; }
    public decimal? Maxima { get; private set; }
    public decimal? Minima { get; private set; }
    public decimal Quantidade { get; private set; }
    public decimal Volume { get; private set; }
    public int Negocios { get; private set; }
    public DateTimeOffset? UltimoNegocioEm { get; private set; }

    public decimal? Vwap => Quantidade == 0 ? null : Math.Round(Volume / Quantidade, 6);
    public decimal? Variacao => Abertura is null or 0 || Ultimo is null ? null : Math.Round((Ultimo.Value / Abertura.Value - 1) * 100, 4);

    public void Registrar(Negocio n)
    {
        Abertura ??= n.Preco;
        Maxima = Maxima is null ? n.Preco : Math.Max(Maxima.Value, n.Preco);
        Minima = Minima is null ? n.Preco : Math.Min(Minima.Value, n.Preco);
        Ultimo = n.Preco;
        Quantidade += n.Quantidade;
        Volume += n.Volume;
        Negocios++;
        UltimoNegocioEm = n.Momento;
    }
}
