using System.Collections.Concurrent;
using LivroDeOfertas.Core.Dados;
using LivroDeOfertas.Core.Dominio;

namespace LivroDeOfertas.Core.Motor;

/// <summary>
/// Conjunto de motores, um por instrumento, com sequência global de eventos, histórico de negócios,
/// candles e estatísticas. Serializa o acesso a cada motor com um lock por instrumento.
/// </summary>
public sealed class Bolsa
{
    private readonly ConcurrentDictionary<string, Mercado> _mercados = new(StringComparer.OrdinalIgnoreCase);
    private readonly IRelogio _relogio;
    private readonly IRegistroDeEventos _registro;
    private long _ultimoId;
    private long _ultimaSequencia;

    public Bolsa(IRelogio? relogio = null, IRegistroDeEventos? registro = null, bool prevenirAutoNegociacao = true)
    {
        _relogio = relogio ?? new RelogioDoSistema();
        _registro = registro ?? new RegistroEmMemoria();
        PrevenirAutoNegociacao = prevenirAutoNegociacao;
    }

    public bool PrevenirAutoNegociacao { get; }
    public IRegistroDeEventos Registro => _registro;
    public IEnumerable<Instrumento> Instrumentos => _mercados.Values.Select(m => m.Motor.Instrumento).OrderBy(i => i.Codigo);

    /// <summary>Disparado depois de cada comando com os eventos gerados, já fora do lock.</summary>
    public event Action<IReadOnlyList<Evento>>? EventosPublicados;

    public Instrumento Listar(Instrumento instrumento)
    {
        _mercados.GetOrAdd(instrumento.Codigo, _ => new Mercado(new MotorDeCasamento(instrumento, _relogio, ProximoId, ProximaSequencia) { PrevenirAutoNegociacao = PrevenirAutoNegociacao }));
        return instrumento;
    }

    public bool Existe(string instrumento) => _mercados.ContainsKey(instrumento);

    public Resultado Enviar(NovaOrdem pedido) => Executar(pedido.Instrumento, m => m.Motor.Enviar(pedido));

    public Resultado Cancelar(string instrumento, CancelarOrdem pedido) => Executar(instrumento, m => m.Motor.Cancelar(pedido));

    public Resultado Alterar(string instrumento, AlterarOrdem pedido) => Executar(instrumento, m => m.Motor.Alterar(pedido));

    public FotoDoLivro Foto(string instrumento, int niveis = 10)
    {
        var m = Obter(instrumento);
        lock (m.Trava) return m.Motor.Livro.Foto(niveis);
    }

    /// <summary>Foto atual de qualquer ordem já vista pelo instrumento (ativa ou encerrada).</summary>
    public FotoDaOrdem? Ordem(string instrumento, long id)
    {
        var m = Obter(instrumento);
        lock (m.Trava) return m.Ordens.TryGetValue(id, out var o) ? o.Foto() : null;
    }

    public IReadOnlyList<FotoDaOrdem> OrdensDaConta(string instrumento, string conta, bool somenteAtivas = true)
    {
        var m = Obter(instrumento);
        lock (m.Trava) return m.Ordens.Values.Where(o => o.Conta == conta && (!somenteAtivas || o.EstaAtiva)).Select(o => o.Foto()).ToArray();
    }

    public IReadOnlyList<Negocio> Negocios(string instrumento, int ultimos = 50)
    {
        var m = Obter(instrumento);
        lock (m.Trava) return m.Negocios.Skip(Math.Max(0, m.Negocios.Count - ultimos)).ToArray();
    }

    public IReadOnlyList<Candle> Candles(string instrumento, TimeSpan intervalo, int ultimos = 100)
    {
        var m = Obter(instrumento);
        lock (m.Trava)
        {
            if (!m.Candles.TryGetValue(intervalo, out var agg))
            {
                agg = new AgregadorDeCandles(intervalo);
                foreach (var n in m.Negocios) agg.Registrar(n);
                m.Candles[intervalo] = agg;
            }
            return agg.Ultimos(ultimos).ToArray();
        }
    }

    public Estatisticas Estatisticas(string instrumento)
    {
        var m = Obter(instrumento);
        lock (m.Trava) return m.Estatisticas;
    }

    /// <summary>
    /// Reconstrói o estado a partir de um registro de eventos, reenviando os comandos na ordem original.
    /// Como ids e sequências são determinísticos, o livro reconstruído é idêntico ao original.
    /// </summary>
    public static Bolsa Reconstruir(IEnumerable<Evento> eventos, IEnumerable<Instrumento> instrumentos)
    {
        var relogio = new RelogioFixo(DateTimeOffset.UnixEpoch);
        var bolsa = new Bolsa(relogio, new RegistroEmMemoria());
        foreach (var i in instrumentos) bolsa.Listar(i);
        foreach (var e in eventos)
        {
            relogio.Definir(e.Momento);
            switch (e)
            {
                case OrdemAceita a:
                    bolsa.Enviar(new NovaOrdem(a.Ordem.Instrumento, a.Ordem.Conta, a.Ordem.Lado, a.Ordem.Tipo, a.Ordem.Quantidade, a.Ordem.Preco, a.Ordem.Validade, a.Ordem.IdCliente));
                    break;
                case OrdemRejeitada r:
                    bolsa.Enviar(new NovaOrdem(r.Ordem.Instrumento, r.Ordem.Conta, r.Ordem.Lado, r.Ordem.Tipo, r.Ordem.Quantidade, r.Ordem.Preco, r.Ordem.Validade, r.Ordem.IdCliente));
                    break;
                case OrdemCancelada c when c.Motivo == "cancelada pelo cliente":
                    bolsa.Cancelar(c.Instrumento, new CancelarOrdem(c.Ordem.Id, c.Ordem.Conta));
                    break;
                case OrdemAlterada al:
                    bolsa.Alterar(al.Instrumento, new AlterarOrdem(al.Ordem.Id, al.Ordem.Conta,
                        al.Ordem.Preco != al.PrecoAnterior ? al.Ordem.Preco : null,
                        al.Ordem.Quantidade != al.QuantidadeAnterior ? al.Ordem.Quantidade : null));
                    break;
            }
        }
        return bolsa;
    }

    private Resultado Executar(string instrumento, Func<Mercado, Resultado> acao)
    {
        var m = Obter(instrumento);
        Resultado r;
        lock (m.Trava)
        {
            r = acao(m);
            if (r.Ordem is not null) m.Ordens[r.Ordem.Id] = r.Ordem;
            foreach (var e in r.Eventos)
            {
                _registro.Anexar(e);
                if (e is NegocioExecutado n)
                {
                    m.Negocios.Add(n.Negocio);
                    m.Estatisticas.Registrar(n.Negocio);
                    foreach (var agg in m.Candles.Values) agg.Registrar(n.Negocio);
                }
            }
        }
        if (r.Eventos.Count > 0) EventosPublicados?.Invoke(r.Eventos);
        return r;
    }

    private Mercado Obter(string instrumento) => _mercados.TryGetValue(instrumento, out var m) ? m : throw new KeyNotFoundException($"instrumento não listado: {instrumento}");

    private long ProximoId() => Interlocked.Increment(ref _ultimoId);
    private long ProximaSequencia() => Interlocked.Increment(ref _ultimaSequencia);

    private sealed class Mercado
    {
        public Mercado(MotorDeCasamento motor) { Motor = motor; Estatisticas = new Estatisticas(motor.Instrumento.Codigo); }
        public MotorDeCasamento Motor { get; }
        public object Trava { get; } = new();
        public List<Negocio> Negocios { get; } = new();
        public Dictionary<TimeSpan, AgregadorDeCandles> Candles { get; } = new();
        public Estatisticas Estatisticas { get; }
        public Dictionary<long, Ordem> Ordens { get; } = new();
    }
}
