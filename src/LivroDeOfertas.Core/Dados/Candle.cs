using LivroDeOfertas.Core.Dominio;

namespace LivroDeOfertas.Core.Dados;

/// <summary>Barra OHLCV de um intervalo de tempo.</summary>
public sealed class Candle
{
    public Candle(string instrumento, DateTimeOffset inicio, TimeSpan intervalo, Negocio primeiro)
    {
        Instrumento = instrumento;
        Inicio = inicio;
        Intervalo = intervalo;
        Abertura = Maxima = Minima = Fechamento = primeiro.Preco;
        Registrar(primeiro);
    }

    public string Instrumento { get; }
    public DateTimeOffset Inicio { get; }
    public TimeSpan Intervalo { get; }
    public DateTimeOffset Fim => Inicio + Intervalo;
    public decimal Abertura { get; }
    public decimal Maxima { get; private set; }
    public decimal Minima { get; private set; }
    public decimal Fechamento { get; private set; }
    public decimal Quantidade { get; private set; }
    public decimal Volume { get; private set; }
    public int Negocios { get; private set; }

    /// <summary>Preço médio ponderado pela quantidade.</summary>
    public decimal Vwap => Quantidade == 0 ? 0 : Math.Round(Volume / Quantidade, 6);

    internal void Registrar(Negocio n)
    {
        if (n.Preco > Maxima) Maxima = n.Preco;
        if (n.Preco < Minima) Minima = n.Preco;
        Fechamento = n.Preco;
        Quantidade += n.Quantidade;
        Volume += n.Volume;
        Negocios++;
    }

    public bool Contem(DateTimeOffset momento) => momento >= Inicio && momento < Fim;
}
