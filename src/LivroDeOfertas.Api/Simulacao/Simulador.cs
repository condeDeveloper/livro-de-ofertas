using LivroDeOfertas.Api.Configuracao;
using LivroDeOfertas.Core.Dominio;
using LivroDeOfertas.Core.Motor;
using Microsoft.Extensions.Options;

namespace LivroDeOfertas.Api.Simulacao;

/// <summary>
/// Gera fluxo de ordens sintético para demonstração: contas fictícias enviam ordens limitadas em torno
/// do último preço (ou do preço inicial), com algumas ordens a mercado e cancelamentos, num passeio
/// aleatório suave. Desligue em Simulador:Ativo.
/// </summary>
public sealed class Simulador : BackgroundService
{
    private readonly Bolsa _bolsa;
    private readonly BolsaOptions _bolsaOptions;
    private readonly SimuladorOptions _options;
    private readonly ILogger<Simulador> _log;
    private readonly Random _rnd = new();
    private readonly Dictionary<string, decimal> _centro = new();
    private readonly List<(string Instrumento, long Id, string Conta)> _ativas = new();

    public Simulador(Bolsa bolsa, IOptions<BolsaOptions> bolsaOptions, IOptions<SimuladorOptions> options, ILogger<Simulador> log)
    {
        _bolsa = bolsa;
        _bolsaOptions = bolsaOptions.Value;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_options.Ativo) return;
        foreach (var i in _bolsaOptions.Instrumentos) _centro[i.Codigo.ToUpperInvariant()] = i.PrecoInicial;
        _log.LogInformation("simulador ligado: {Ops} ordens/s em {N} instrumentos", _options.OrdensPorSegundo, _centro.Count);

        var intervalo = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, _options.OrdensPorSegundo));
        using var timer = new PeriodicTimer(intervalo);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { Passo(); }
            catch (Exception e) { _log.LogWarning(e, "passo do simulador falhou"); }
        }
    }

    private void Passo()
    {
        var instrumento = _bolsa.Instrumentos.ElementAt(_rnd.Next(_centro.Count));
        var conta = $"SIM{_rnd.Next(1, _options.Contas + 1):000}";
        var codigo = instrumento.Codigo;

        // centro do passeio aleatório acompanha o último negócio
        var ultimo = _bolsa.Estatisticas(codigo).Ultimo;
        if (ultimo is not null) _centro[codigo] = ultimo.Value;
        _centro[codigo] = Math.Max(instrumento.TickPreco * 10, _centro[codigo] * (1 + (decimal)(_rnd.NextDouble() - 0.5) * 0.002m));

        var sorteio = _rnd.NextDouble();
        if (sorteio < 0.12 && _ativas.Count > 20)
        {
            var (inst, id, cta) = _ativas[_rnd.Next(_ativas.Count)];
            _ativas.RemoveAll(a => a.Id == id);
            _bolsa.Cancelar(inst, new CancelarOrdem(id, cta));
            return;
        }

        var lado = _rnd.NextDouble() < 0.5 ? Lado.Compra : Lado.Venda;
        var lotes = _rnd.Next(1, 20);
        var quantidade = instrumento.LoteMinimo * lotes;

        if (sorteio < 0.22)
        {
            _bolsa.Enviar(new NovaOrdem(codigo, conta, lado, TipoOrdem.Mercado, quantidade, null, Validade.ExecutaOuCancela));
            return;
        }

        // ordens limitadas espalhadas em torno do centro: quanto mais longe, menos frequentes
        var afastamentoTicks = (int)Math.Round(Math.Abs(GaussianaAproximada()) * 6);
        var sinal = lado == Lado.Compra ? -1 : 1;
        var preco = instrumento.ArredondarPreco(_centro[codigo] + sinal * afastamentoTicks * instrumento.TickPreco);
        if (_rnd.NextDouble() < 0.15) preco = instrumento.ArredondarPreco(_centro[codigo] - sinal * instrumento.TickPreco); // agressiva: cruza o spread

        var r = _bolsa.Enviar(new NovaOrdem(codigo, conta, lado, TipoOrdem.Limitada, quantidade, preco));
        if (r.Ordem is { EstaAtiva: true }) _ativas.Add((codigo, r.Ordem.Id, conta));
        if (_ativas.Count > 400) _ativas.RemoveRange(0, 100);
    }

    /// <summary>Soma de três uniformes: aproximação barata de uma normal para espalhar os preços.</summary>
    private double GaussianaAproximada() => (_rnd.NextDouble() + _rnd.NextDouble() + _rnd.NextDouble() - 1.5) * 1.15;
}
