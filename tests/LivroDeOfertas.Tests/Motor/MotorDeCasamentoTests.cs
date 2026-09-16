using FluentAssertions;
using LivroDeOfertas.Core.Dominio;
using LivroDeOfertas.Core.Motor;

namespace LivroDeOfertas.Tests.Motor;

public class MotorDeCasamentoTests
{
    private static readonly Instrumento Petr = new("CONDE3", 0.01m, 100);
    private readonly RelogioFixo _relogio = new(new DateTimeOffset(2026, 9, 15, 13, 0, 0, TimeSpan.Zero));
    private readonly Bolsa _bolsa;

    public MotorDeCasamentoTests()
    {
        _bolsa = new Bolsa(_relogio);
        _bolsa.Listar(Petr);
    }

    private Resultado Limitada(string conta, Lado lado, decimal qtd, decimal preco, Validade validade = Validade.AteCancelar) =>
        _bolsa.Enviar(new NovaOrdem("CONDE3", conta, lado, TipoOrdem.Limitada, qtd, preco, validade));

    private Resultado Mercado(string conta, Lado lado, decimal qtd, Validade validade = Validade.ExecutaOuCancela) =>
        _bolsa.Enviar(new NovaOrdem("CONDE3", conta, lado, TipoOrdem.Mercado, qtd, null, validade));

    [Fact]
    public void OrdemSemContraparteFicaNoLivro()
    {
        var r = Limitada("A", Lado.Compra, 100, 42.50m);
        r.Aceita.Should().BeTrue();
        r.Negocios.Should().BeEmpty();
        r.Ordem!.Situacao.Should().Be(SituacaoOrdem.Nova);
        var foto = _bolsa.Foto("CONDE3");
        foto.MelhorCompra.Should().Be(42.50m);
        foto.Compras[0].Quantidade.Should().Be(100);
    }

    [Fact]
    public void CruzamentoExecutaAoPrecoDaOrdemPassiva()
    {
        Limitada("A", Lado.Venda, 300, 42.55m);
        var r = Limitada("B", Lado.Compra, 200, 42.60m); // agressora paga até 42,60 mas negocia a 42,55

        r.Negocios.Should().ContainSingle();
        var n = r.Negocios.Single();
        n.Preco.Should().Be(42.55m);
        n.Quantidade.Should().Be(200);
        n.Agressor.Should().Be(Lado.Compra);
        n.ContaCompra.Should().Be("B");
        n.ContaVenda.Should().Be("A");
        r.Ordem!.Situacao.Should().Be(SituacaoOrdem.Executada);

        var foto = _bolsa.Foto("CONDE3");
        foto.Vendas[0].Quantidade.Should().Be(100); // sobrou 100 da venda
        foto.Compras.Should().BeEmpty();
    }

    [Fact]
    public void PrioridadePrecoDepoisTempo()
    {
        Limitada("A", Lado.Venda, 100, 42.60m);
        _relogio.Avancar(TimeSpan.FromSeconds(1));
        Limitada("B", Lado.Venda, 100, 42.55m); // melhor preço executa primeiro
        _relogio.Avancar(TimeSpan.FromSeconds(1));
        Limitada("C", Lado.Venda, 100, 42.55m); // mesmo preço que B, chegou depois

        Limitada("D", Lado.Venda, 200, 42.60m); // mesmo preço que A, chegou depois

        var r = Limitada("E", Lado.Compra, 300, 42.60m);
        var negocios = r.Negocios.ToList();
        negocios.Should().HaveCount(3);
        negocios[0].ContaVenda.Should().Be("B");
        negocios[0].Preco.Should().Be(42.55m);
        negocios[1].ContaVenda.Should().Be("C");
        negocios[2].ContaVenda.Should().Be("A");
        negocios[2].Preco.Should().Be(42.60m);
        negocios[2].Quantidade.Should().Be(100);
        var venda = _bolsa.Foto("CONDE3").Vendas.Single();
        venda.Preco.Should().Be(42.60m);
        venda.Quantidade.Should().Be(200); // só a de D sobrou
        venda.Ordens.Should().Be(1);
    }

    [Fact]
    public void OrdemAMercadoVarreNiveisECancelaOResto()
    {
        Limitada("A", Lado.Venda, 100, 42.55m);
        Limitada("B", Lado.Venda, 100, 42.70m);
        var r = Mercado("C", Lado.Compra, 500);

        r.Negocios.Should().HaveCount(2);
        r.Negocios.Sum(n => n.Quantidade).Should().Be(200);
        r.Ordem!.Situacao.Should().Be(SituacaoOrdem.Cancelada);
        r.Ordem.QuantidadeExecutada.Should().Be(200);
        r.Motivo.Should().Contain("sem liquidez");
        _bolsa.Foto("CONDE3").Vendas.Should().BeEmpty();
        _bolsa.Foto("CONDE3").Compras.Should().BeEmpty(); // ordem a mercado nunca fica no livro
    }

    [Fact]
    public void IocExecutaOQuePodeECancelaORestante()
    {
        Limitada("A", Lado.Venda, 100, 42.55m);
        var r = Limitada("B", Lado.Compra, 300, 42.55m, Validade.ExecutaOuCancela);
        r.Negocios.Single().Quantidade.Should().Be(100);
        r.Ordem!.Situacao.Should().Be(SituacaoOrdem.Cancelada);
        r.Motivo.Should().Be("IOC: restante cancelado");
        _bolsa.Foto("CONDE3").Compras.Should().BeEmpty();
    }

    [Fact]
    public void FokSoExecutaSePreencherTudo()
    {
        Limitada("A", Lado.Venda, 100, 42.55m);
        Limitada("B", Lado.Venda, 100, 42.56m);

        var falha = Limitada("C", Lado.Compra, 300, 42.60m, Validade.TudoOuNada);
        falha.Negocios.Should().BeEmpty();
        falha.Ordem!.Situacao.Should().Be(SituacaoOrdem.Cancelada);
        _bolsa.Foto("CONDE3").Vendas.Sum(v => v.Quantidade).Should().Be(200); // livro intocado

        var sucesso = Limitada("C", Lado.Compra, 200, 42.60m, Validade.TudoOuNada);
        sucesso.Negocios.Should().HaveCount(2);
        sucesso.Ordem!.Situacao.Should().Be(SituacaoOrdem.Executada);
    }

    [Fact]
    public void FokNaoContaLiquidezForaDoPreco()
    {
        Limitada("A", Lado.Venda, 100, 42.55m);
        Limitada("B", Lado.Venda, 100, 43.00m); // caro demais para a compra a 42,60
        var r = Limitada("C", Lado.Compra, 200, 42.60m, Validade.TudoOuNada);
        r.Ordem!.Situacao.Should().Be(SituacaoOrdem.Cancelada);
    }

    [Fact]
    public void CancelarRemoveDoLivroERecusaOutraConta()
    {
        var r = Limitada("A", Lado.Compra, 100, 42.50m);
        var id = r.Ordem!.Id;

        var act = () => _bolsa.Cancelar("CONDE3", new CancelarOrdem(id, "B"));
        act.Should().Throw<ContaDiferenteException>();

        var c = _bolsa.Cancelar("CONDE3", new CancelarOrdem(id, "A"));
        c.Ordem!.Situacao.Should().Be(SituacaoOrdem.Cancelada);
        _bolsa.Foto("CONDE3").Compras.Should().BeEmpty();
        _bolsa.Cancelar("CONDE3", new CancelarOrdem(id, "A")).Ordem.Should().BeNull(); // já não está ativa
    }

    [Fact]
    public void ReduzirQuantidadeMantemPrioridadeAumentarPerde()
    {
        var primeira = Limitada("A", Lado.Compra, 300, 42.50m).Ordem!;
        var segunda = Limitada("B", Lado.Compra, 100, 42.50m).Ordem!;

        _bolsa.Alterar("CONDE3", new AlterarOrdem(primeira.Id, "A", NovaQuantidade: 200)).Eventos.OfType<OrdemAlterada>().Single().PerdeuPrioridade.Should().BeFalse();
        var venda1 = Limitada("C", Lado.Venda, 100, 42.50m);
        venda1.Negocios.Single().ContaCompra.Should().Be("A"); // A continua na frente

        _bolsa.Alterar("CONDE3", new AlterarOrdem(primeira.Id, "A", NovaQuantidade: 500)).Eventos.OfType<OrdemAlterada>().Single().PerdeuPrioridade.Should().BeTrue();
        var venda2 = Limitada("C", Lado.Venda, 100, 42.50m);
        venda2.Negocios.Single().ContaCompra.Should().Be("B"); // agora B está na frente
        segunda.Situacao.Should().Be(SituacaoOrdem.Executada);
    }

    [Fact]
    public void AlterarPrecoPodeCruzarImediatamente()
    {
        Limitada("A", Lado.Venda, 100, 42.60m);
        var compra = Limitada("B", Lado.Compra, 100, 42.50m).Ordem!;
        var r = _bolsa.Alterar("CONDE3", new AlterarOrdem(compra.Id, "B", NovoPreco: 42.60m));
        r.Negocios.Single().Preco.Should().Be(42.60m);
        compra.Situacao.Should().Be(SituacaoOrdem.Executada);
        _bolsa.Foto("CONDE3").Compras.Should().BeEmpty();
        _bolsa.Foto("CONDE3").Vendas.Should().BeEmpty();
    }

    [Fact]
    public void PrevencaoDeAutoNegociacaoCancelaAPassiva()
    {
        var passiva = Limitada("A", Lado.Venda, 100, 42.55m).Ordem!;
        Limitada("B", Lado.Venda, 100, 42.55m);
        var r = Limitada("A", Lado.Compra, 100, 42.55m);

        passiva.Situacao.Should().Be(SituacaoOrdem.Cancelada);
        passiva.MotivoEncerramento.Should().Contain("auto-negociação");
        r.Negocios.Single().ContaVenda.Should().Be("B");
        r.Eventos.OfType<OrdemCancelada>().Should().ContainSingle(e => e.Ordem.Id == passiva.Id);
    }

    [Fact]
    public void SemPrevencaoContasIguaisNegociam()
    {
        var bolsa = new Bolsa(_relogio, prevenirAutoNegociacao: false);
        bolsa.Listar(Petr);
        bolsa.Enviar(new NovaOrdem("CONDE3", "A", Lado.Venda, TipoOrdem.Limitada, 100, 42.55m));
        var r = bolsa.Enviar(new NovaOrdem("CONDE3", "A", Lado.Compra, TipoOrdem.Limitada, 100, 42.55m));
        r.Negocios.Should().ContainSingle();
    }

    [Fact]
    public void RejeitaPrecoForaDoTickEQuantidadeForaDoLote()
    {
        Limitada("A", Lado.Compra, 100, 42.505m).Ordem!.Situacao.Should().Be(SituacaoOrdem.Rejeitada);
        var r = Limitada("A", Lado.Compra, 150, 42.50m);
        r.Aceita.Should().BeFalse();
        r.Motivo.Should().Contain("lote");
        _bolsa.Foto("CONDE3").Compras.Should().BeEmpty();
    }

    [Fact]
    public void OrdemAMercadoNaoPodeSerGtc()
    {
        var act = () => Mercado("A", Lado.Compra, 100, Validade.AteCancelar);
        act.Should().Throw<OrdemInvalidaException>().WithMessage("*IOC ou FOK*");
    }

    [Fact]
    public void LivroNuncaFicaCruzadoEQuantidadeSeConserva()
    {
        var rnd = new Random(7);
        decimal enviado = 0, executado = 0, cancelado = 0;
        var ativas = new List<(long Id, string Conta)>();
        for (var i = 0; i < 5000; i++)
        {
            var conta = "C" + rnd.Next(1, 6);
            var lado = rnd.Next(2) == 0 ? Lado.Compra : Lado.Venda;
            var qtd = 100m * rnd.Next(1, 10);
            Resultado r;
            if (rnd.NextDouble() < 0.1 && ativas.Count > 0)
            {
                var (id, cta) = ativas[rnd.Next(ativas.Count)];
                ativas.RemoveAll(a => a.Id == id);
                r = _bolsa.Cancelar("CONDE3", new CancelarOrdem(id, cta));
                if (r.Ordem is not null) cancelado += r.Ordem.QuantidadeRestante;
                continue;
            }
            if (rnd.NextDouble() < 0.15)
                r = Mercado(conta, lado, qtd);
            else
                r = Limitada(conta, lado, qtd, 42.00m + rnd.Next(0, 101) * 0.01m, rnd.NextDouble() < 0.1 ? Validade.ExecutaOuCancela : Validade.AteCancelar);
            enviado += qtd;
            executado += r.Negocios.Sum(n => n.Quantidade) * 2; // cada negócio consome quantidade de duas ordens
            if (r.Ordem is { Situacao: SituacaoOrdem.Cancelada }) cancelado += r.Ordem.QuantidadeRestante;
            foreach (var c in r.Eventos.OfType<OrdemCancelada>().Where(e => e.Ordem.Id != r.Ordem!.Id)) cancelado += c.Ordem.QuantidadeRestante; // auto-negociação
            if (r.Ordem is { EstaAtiva: true }) ativas.Add((r.Ordem.Id, conta));

            var foto = _bolsa.Foto("CONDE3");
            if (foto.MelhorCompra is not null && foto.MelhorVenda is not null) foto.MelhorCompra.Should().BeLessThan(foto.MelhorVenda.Value);
        }
        var noLivro = _bolsa.Foto("CONDE3", 1000).Compras.Sum(c => c.Quantidade) + _bolsa.Foto("CONDE3", 1000).Vendas.Sum(v => v.Quantidade);
        (executado + cancelado + noLivro).Should().Be(enviado);
    }

    [Fact]
    public void ReconstruirAPartirDosEventosReproduzOLivro()
    {
        Limitada("A", Lado.Venda, 300, 42.55m);
        Limitada("B", Lado.Compra, 100, 42.50m);
        var alterada = Limitada("C", Lado.Compra, 200, 42.40m).Ordem!;
        _bolsa.Alterar("CONDE3", new AlterarOrdem(alterada.Id, "C", NovoPreco: 42.55m));
        var cancelar = Limitada("D", Lado.Venda, 100, 42.70m).Ordem!;
        _bolsa.Cancelar("CONDE3", new CancelarOrdem(cancelar.Id, "D"));
        Mercado("E", Lado.Compra, 50);
        Limitada("F", Lado.Compra, 150, 42.50m).Ordem!.Situacao.Should().Be(SituacaoOrdem.Rejeitada);

        var copia = Bolsa.Reconstruir(_bolsa.Registro.Todos(), _bolsa.Instrumentos);

        copia.Foto("CONDE3", 50).Should().BeEquivalentTo(_bolsa.Foto("CONDE3", 50));
        copia.Negocios("CONDE3").Should().BeEquivalentTo(_bolsa.Negocios("CONDE3"));
        copia.Registro.UltimaSequencia.Should().Be(_bolsa.Registro.UltimaSequencia);
    }
}
