using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LivroDeOfertas.Tests.Api;

public class ApiTests : IClassFixture<ApiTests.Fabrica>
{
    public sealed class Fabrica : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Simulador:Ativo"] = "false" }));
    }

    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ApiTests(Fabrica fabrica) => _http = fabrica.CreateClient();

    private async Task<JsonElement> Post(string url, object body)
    {
        var r = await _http.PostAsJsonAsync(url, body);
        var json = await r.Content.ReadFromJsonAsync<JsonElement>(Json);
        json.TryGetProperty("statusCode", out _);
        return JsonDocument.Parse($"{{\"status\":{(int)r.StatusCode},\"body\":{json.GetRawText()}}}").RootElement;
    }

    [Fact]
    public async Task ListaInstrumentosDaConfiguracao()
    {
        var lista = await _http.GetFromJsonAsync<JsonElement>("/api/instrumentos", Json);
        lista.EnumerateArray().Select(i => i.GetProperty("codigo").GetString()).Should().Contain(new[] { "CONDE3", "JOGO4", "TESO11" });
    }

    [Fact]
    public async Task EnviaOrdensECruzaPelaApi()
    {
        var venda = await Post("/api/ordens", new { instrumento = "JOGO4", conta = "api-a", lado = "Venda", tipo = "Limitada", quantidade = 300, preco = 12.80 });
        venda.GetProperty("status").GetInt32().Should().Be(201);
        venda.GetProperty("body").GetProperty("negocios").GetArrayLength().Should().Be(0);

        var compra = await Post("/api/ordens", new { instrumento = "JOGO4", conta = "api-b", lado = "Compra", tipo = "Limitada", quantidade = 100, preco = 12.85 });
        compra.GetProperty("status").GetInt32().Should().Be(201);
        var negocios = compra.GetProperty("body").GetProperty("negocios");
        negocios.GetArrayLength().Should().Be(1);
        negocios[0].GetProperty("preco").GetDecimal().Should().Be(12.80m);
        compra.GetProperty("body").GetProperty("ordem").GetProperty("situacao").GetString().Should().Be("Executada");

        var livro = await _http.GetFromJsonAsync<JsonElement>("/api/livro/JOGO4", Json);
        livro.GetProperty("vendas")[0].GetProperty("quantidade").GetDecimal().Should().Be(200);

        var negociosApi = await _http.GetFromJsonAsync<JsonElement>("/api/negocios/JOGO4", Json);
        negociosApi.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var stats = await _http.GetFromJsonAsync<JsonElement>("/api/estatisticas/JOGO4", Json);
        stats.GetProperty("ultimo").GetDecimal().Should().Be(12.80m);

        var candles = await _http.GetFromJsonAsync<JsonElement>("/api/candles/JOGO4?intervalo=1m", Json);
        candles.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task RejeicoesEErros()
    {
        var fora = await Post("/api/ordens", new { instrumento = "TESO11", conta = "x", lado = "Compra", tipo = "Limitada", quantidade = 10, preco = 105.301 });
        fora.GetProperty("status").GetInt32().Should().Be(422);
        fora.GetProperty("body").GetProperty("ordem").GetProperty("motivo").GetString().Should().Contain("tick");

        var inexistente = await _http.PostAsJsonAsync("/api/ordens", new { instrumento = "NAOEXISTE", conta = "x", lado = "Compra", tipo = "Limitada", quantidade = 100, preco = 1 });
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var mercadoGtc = await _http.PostAsJsonAsync("/api/ordens", new { instrumento = "TESO11", conta = "x", lado = "Compra", tipo = "Mercado", quantidade = 10, validade = "AteCancelar" });
        mercadoGtc.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var criada = await Post("/api/ordens", new { instrumento = "TESO11", conta = "dono", lado = "Compra", tipo = "Limitada", quantidade = 10, preco = 100.00 });
        var id = criada.GetProperty("body").GetProperty("ordem").GetProperty("id").GetInt64();
        var outraConta = await _http.DeleteAsync($"/api/ordens/TESO11/{id}?conta=intruso");
        outraConta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var ok = await _http.DeleteAsync($"/api/ordens/TESO11/{id}?conta=dono");
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var consulta = await _http.GetFromJsonAsync<JsonElement>($"/api/ordens/TESO11/{id}", Json);
        consulta.GetProperty("situacao").GetString().Should().Be("Cancelada");
    }

    [Fact]
    public async Task DocsESaude()
    {
        (await _http.GetAsync("/saude")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _http.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);
        var eventos = await _http.GetFromJsonAsync<JsonElement>("/api/eventos?limite=5", Json);
        eventos.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
