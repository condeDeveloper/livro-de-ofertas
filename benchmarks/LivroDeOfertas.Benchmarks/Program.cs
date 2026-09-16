using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using LivroDeOfertas.Core.Dominio;
using LivroDeOfertas.Core.Motor;

BenchmarkRunner.Run<MotorBenchmarks>();

/// <summary>
/// Mede o custo por ordem em três cenários: livro profundo sem cruzamento, fluxo misto realista
/// e varredura de mercado. Rode com: dotnet run -c Release --project benchmarks/LivroDeOfertas.Benchmarks
/// </summary>
[MemoryDiagnoser]
public class MotorBenchmarks
{
    private const int N = 100_000;
    private NovaOrdem[] _passivas = Array.Empty<NovaOrdem>();
    private NovaOrdem[] _mistas = Array.Empty<NovaOrdem>();

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(1);
        _passivas = Enumerable.Range(0, N).Select(i => new NovaOrdem("BENCH", "C" + (i % 50),
            i % 2 == 0 ? Lado.Compra : Lado.Venda, TipoOrdem.Limitada, 100,
            i % 2 == 0 ? 99.00m - rnd.Next(0, 200) * 0.01m : 101.00m + rnd.Next(0, 200) * 0.01m)).ToArray();

        _mistas = Enumerable.Range(0, N).Select(i =>
        {
            var lado = rnd.Next(2) == 0 ? Lado.Compra : Lado.Venda;
            if (rnd.NextDouble() < 0.1) return new NovaOrdem("BENCH", "C" + rnd.Next(50), lado, TipoOrdem.Mercado, 100m * rnd.Next(1, 5), null, Validade.ExecutaOuCancela);
            return new NovaOrdem("BENCH", "C" + rnd.Next(50), lado, TipoOrdem.Limitada, 100m * rnd.Next(1, 5), 99.00m + rnd.Next(0, 201) * 0.01m);
        }).ToArray();
    }

    private static Bolsa NovaBolsa()
    {
        var b = new Bolsa(new RelogioFixo(DateTimeOffset.UnixEpoch), prevenirAutoNegociacao: false);
        b.Listar(new Instrumento("BENCH", 0.01m, 100));
        return b;
    }

    [Benchmark(Description = "100k ordens passivas (só inserção no livro)")]
    public int Passivas()
    {
        var b = NovaBolsa();
        var negocios = 0;
        foreach (var o in _passivas) negocios += b.Enviar(o).Eventos.Count;
        return negocios;
    }

    [Benchmark(Description = "100k ordens mistas (limitadas cruzando + 10% a mercado)")]
    public int Mistas()
    {
        var b = NovaBolsa();
        var negocios = 0;
        foreach (var o in _mistas) negocios += b.Enviar(o).Eventos.Count;
        return negocios;
    }
}
