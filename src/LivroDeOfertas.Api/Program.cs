using System.Text.Json.Serialization;
using LivroDeOfertas.Api.Configuracao;
using LivroDeOfertas.Api.Endpoints;
using LivroDeOfertas.Api.Simulacao;
using LivroDeOfertas.Api.TempoReal;
using LivroDeOfertas.Core.Dominio;
using LivroDeOfertas.Core.Motor;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BolsaOptions>(builder.Configuration.GetSection(BolsaOptions.Secao));
builder.Services.Configure<SimuladorOptions>(builder.Configuration.GetSection(SimuladorOptions.Secao));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Livro de Ofertas",
        Version = "v1",
        Description = "Motor de casamento de ofertas com prioridade preço-tempo: ordens limitadas e a mercado, GTC/IOC/FOK, alteração e cancelamento, "
                      + "prevenção de auto-negociação, candles, estatísticas e registro de eventos. Tempo real via SignalR em /hub/mercado.",
    });
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true).AllowCredentials()));

builder.Services.AddSingleton(sp =>
{
    var opt = sp.GetRequiredService<IOptions<BolsaOptions>>().Value;
    var bolsa = new Bolsa(prevenirAutoNegociacao: opt.PrevenirAutoNegociacao);
    foreach (var i in opt.Instrumentos) bolsa.Listar(new Instrumento(i.Codigo, i.TickPreco, i.LoteMinimo));
    return bolsa;
});
builder.Services.AddHostedService<PublicadorDeMercado>();
builder.Services.AddHostedService<Simulador>();

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(o => { o.RoutePrefix = "docs"; o.DocumentTitle = "Livro de Ofertas"; });
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapOrdens();
app.MapMercado();
app.MapHub<MercadoHub>("/hub/mercado");
app.MapGet("/saude", () => Results.Ok(new { status = "ok", agora = DateTimeOffset.UtcNow }));

app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (OrdemInvalidaException e) { await Problema(ctx, 422, e.Message); }
    catch (ContaDiferenteException e) { await Problema(ctx, 403, e.Message); }
    catch (KeyNotFoundException e) { await Problema(ctx, 404, e.Message); }
});

app.Run();

static async Task Problema(HttpContext ctx, int status, string detalhe)
{
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { title = detalhe, status });
}

public partial class Program { }
