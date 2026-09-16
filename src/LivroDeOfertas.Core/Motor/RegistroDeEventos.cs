using LivroDeOfertas.Core.Dominio;

namespace LivroDeOfertas.Core.Motor;

/// <summary>Registro append-only de eventos: a fonte da verdade que permite reconstruir o estado.</summary>
public interface IRegistroDeEventos
{
    void Anexar(Evento evento);
    IReadOnlyList<Evento> Todos();
    IEnumerable<Evento> Desde(long sequencia);
    long UltimaSequencia { get; }
}

public sealed class RegistroEmMemoria : IRegistroDeEventos
{
    private readonly List<Evento> _eventos = new();
    private readonly object _trava = new();

    public void Anexar(Evento evento)
    {
        lock (_trava) _eventos.Add(evento);
    }

    public IReadOnlyList<Evento> Todos()
    {
        lock (_trava) return _eventos.ToArray();
    }

    public IEnumerable<Evento> Desde(long sequencia)
    {
        lock (_trava) return _eventos.Where(e => e.Sequencia > sequencia).ToArray();
    }

    public long UltimaSequencia
    {
        get { lock (_trava) return _eventos.Count == 0 ? 0 : _eventos[^1].Sequencia; }
    }
}
