using LivroDeOfertas.Core.Dominio;

namespace LivroDeOfertas.Core.Dados;

/// <summary>Agrupa negócios em candles de um intervalo fixo, alinhados ao relógio (ex.: 1 min começa em :00).</summary>
public sealed class AgregadorDeCandles
{
    private readonly List<Candle> _candles = new();
    private readonly int _maximo;

    public AgregadorDeCandles(TimeSpan intervalo, int maximoCandles = 1440)
    {
        if (intervalo <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(intervalo));
        Intervalo = intervalo;
        _maximo = maximoCandles;
    }

    public TimeSpan Intervalo { get; }
    public IReadOnlyList<Candle> Candles => _candles;
    public Candle? Atual => _candles.Count == 0 ? null : _candles[^1];

    public void Registrar(Negocio negocio)
    {
        var inicio = Alinhar(negocio.Momento);
        var atual = Atual;
        if (atual is not null && atual.Inicio == inicio)
        {
            atual.Registrar(negocio);
            return;
        }
        if (atual is not null && inicio < atual.Inicio) throw new InvalidOperationException("negócio fora de ordem cronológica");
        _candles.Add(new Candle(negocio.Instrumento, inicio, Intervalo, negocio));
        if (_candles.Count > _maximo) _candles.RemoveAt(0);
    }

    public IEnumerable<Candle> Ultimos(int n) => _candles.Skip(Math.Max(0, _candles.Count - n));

    private DateTimeOffset Alinhar(DateTimeOffset momento)
    {
        var ticks = momento.UtcTicks - momento.UtcTicks % Intervalo.Ticks;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
}
