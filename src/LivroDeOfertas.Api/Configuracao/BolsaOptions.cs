namespace LivroDeOfertas.Api.Configuracao;

public sealed class BolsaOptions
{
    public const string Secao = "Bolsa";

    public bool PrevenirAutoNegociacao { get; set; } = true;
    public List<InstrumentoOptions> Instrumentos { get; set; } = new();
}

public sealed class InstrumentoOptions
{
    public string Codigo { get; set; } = string.Empty;
    public decimal TickPreco { get; set; } = 0.01m;
    public decimal LoteMinimo { get; set; } = 1m;
    public decimal PrecoInicial { get; set; } = 100m;
}

public sealed class SimuladorOptions
{
    public const string Secao = "Simulador";

    public bool Ativo { get; set; }
    public int OrdensPorSegundo { get; set; } = 5;
    public int Contas { get; set; } = 10;
}
