using FluentAssertions;
using LivroDeOfertas.Core.Dados;
using LivroDeOfertas.Core.Dominio;

namespace LivroDeOfertas.Tests.Dados;

public class CandlesTests
{
    private static Negocio N(decimal preco, decimal qtd, int segundo) =>
        new(1, "CONDE3", preco, qtd, 1, 2, "A", "B", Lado.Compra, new DateTimeOffset(2026, 9, 15, 13, 0, 0, TimeSpan.Zero).AddSeconds(segundo));

    [Fact]
    public void AgrupaPorIntervaloAlinhadoAoRelogio()
    {
        var agg = new AgregadorDeCandles(TimeSpan.FromMinutes(1));
        agg.Registrar(N(10, 100, 5));
        agg.Registrar(N(12, 100, 30));
        agg.Registrar(N(9, 200, 59));
        agg.Registrar(N(11, 100, 61)); // próximo minuto

        agg.Candles.Should().HaveCount(2);
        var c = agg.Candles[0];
        c.Inicio.Should().Be(new DateTimeOffset(2026, 9, 15, 13, 0, 0, TimeSpan.Zero));
        c.Abertura.Should().Be(10);
        c.Maxima.Should().Be(12);
        c.Minima.Should().Be(9);
        c.Fechamento.Should().Be(9);
        c.Quantidade.Should().Be(400);
        c.Volume.Should().Be(10 * 100 + 12 * 100 + 9 * 200);
        c.Vwap.Should().Be(4000m / 400);
        c.Negocios.Should().Be(3);
        agg.Candles[1].Abertura.Should().Be(11);
    }

    [Fact]
    public void RecusaNegocioForaDeOrdem()
    {
        var agg = new AgregadorDeCandles(TimeSpan.FromMinutes(1));
        agg.Registrar(N(10, 100, 61));
        var act = () => agg.Registrar(N(10, 100, 5));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EstatisticasDoPregao()
    {
        var e = new Estatisticas("CONDE3");
        e.Vwap.Should().BeNull();
        e.Registrar(N(10, 100, 1));
        e.Registrar(N(11, 300, 2));
        e.Abertura.Should().Be(10);
        e.Ultimo.Should().Be(11);
        e.Maxima.Should().Be(11);
        e.Minima.Should().Be(10);
        e.Vwap.Should().Be((10m * 100 + 11m * 300) / 400);
        e.Variacao.Should().Be(10m);
        e.Negocios.Should().Be(2);
    }
}
