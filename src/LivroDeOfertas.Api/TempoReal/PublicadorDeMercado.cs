using LivroDeOfertas.Api.Contratos;
using LivroDeOfertas.Core.Dominio;
using LivroDeOfertas.Core.Motor;
using Microsoft.AspNetCore.SignalR;

namespace LivroDeOfertas.Api.TempoReal;

/// <summary>Ouve os eventos da bolsa e repassa aos assinantes do hub, sem bloquear o motor.</summary>
public sealed class PublicadorDeMercado : IHostedService
{
    private readonly Bolsa _bolsa;
    private readonly IHubContext<MercadoHub> _hub;
    private readonly ILogger<PublicadorDeMercado> _log;

    public PublicadorDeMercado(Bolsa bolsa, IHubContext<MercadoHub> hub, ILogger<PublicadorDeMercado> log)
    {
        _bolsa = bolsa;
        _hub = hub;
        _log = log;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _bolsa.EventosPublicados += Publicar;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _bolsa.EventosPublicados -= Publicar;
        return Task.CompletedTask;
    }

    private void Publicar(IReadOnlyList<Evento> eventos)
    {
        if (eventos.Count == 0) return;
        var instrumento = eventos[0].Instrumento;
        _ = Task.Run(async () =>
        {
            try
            {
                var grupo = _hub.Clients.Group(instrumento);
                foreach (var n in eventos.OfType<NegocioExecutado>())
                    await grupo.SendAsync("negocio", NegocioResponse.De(n.Negocio));
                await grupo.SendAsync("livro", _bolsa.Foto(instrumento, 10));
            }
            catch (Exception e)
            {
                _log.LogWarning(e, "falha ao publicar eventos de {Instrumento}", instrumento);
            }
        });
    }
}
