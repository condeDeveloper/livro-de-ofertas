using Microsoft.AspNetCore.SignalR;

namespace LivroDeOfertas.Api.TempoReal;

/// <summary>
/// Hub SignalR. Clientes entram no grupo do instrumento e recebem "livro" (foto após cada comando)
/// e "negocio" (cada execução) em tempo real.
/// </summary>
public sealed class MercadoHub : Hub
{
    public Task Assinar(string instrumento) => Groups.AddToGroupAsync(Context.ConnectionId, instrumento.ToUpperInvariant());

    public Task Cancelar(string instrumento) => Groups.RemoveFromGroupAsync(Context.ConnectionId, instrumento.ToUpperInvariant());
}
