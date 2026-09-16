using FluentAssertions;
using LivroDeOfertas.Core.Dominio;

namespace LivroDeOfertas.Tests.Dominio;

public class InstrumentoTests
{
    [Theory]
    [InlineData(42.50, true)]
    [InlineData(42.505, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void ValidaPrecoPeloTick(double preco, bool esperado) =>
        new Instrumento("CONDE3", 0.01m, 100).PrecoValido((decimal)preco).Should().Be(esperado);

    [Theory]
    [InlineData(100, true)]
    [InlineData(150, false)]
    [InlineData(0, false)]
    public void ValidaQuantidadePeloLote(double qtd, bool esperado) =>
        new Instrumento("CONDE3", 0.01m, 100).QuantidadeValida((decimal)qtd).Should().Be(esperado);

    [Fact]
    public void ArredondaParaOTickMaisProximo()
    {
        var i = new Instrumento("TESO11", 0.05m, 1);
        i.ArredondarPreco(105.32m).Should().Be(105.30m);
        i.ArredondarPreco(105.33m).Should().Be(105.35m);
    }

    [Fact]
    public void NormalizaCodigoERejeitaInvalidos()
    {
        new Instrumento("conde3", 0.01m, 1).Codigo.Should().Be("CONDE3");
        var act = () => new Instrumento("CON DE", 0.01m, 1);
        act.Should().Throw<ArgumentException>();
        var tick = () => new Instrumento("X", 0, 1);
        tick.Should().Throw<ArgumentOutOfRangeException>();
    }
}
