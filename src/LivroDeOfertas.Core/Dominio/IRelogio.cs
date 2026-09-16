namespace LivroDeOfertas.Core.Dominio;

/// <summary>Fonte de tempo injetável, para testes determinísticos e reprodução de eventos.</summary>
public interface IRelogio
{
    DateTimeOffset Agora { get; }
}

public sealed class RelogioDoSistema : IRelogio
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}

/// <summary>Relógio controlado manualmente.</summary>
public sealed class RelogioFixo : IRelogio
{
    public RelogioFixo(DateTimeOffset inicio) => Agora = inicio;
    public DateTimeOffset Agora { get; private set; }
    public void Avancar(TimeSpan delta) => Agora += delta;
    public void Definir(DateTimeOffset momento) => Agora = momento;
}
