using LivroDeOfertas.Core.Dominio;

namespace LivroDeOfertas.Core.Motor;

/// <summary>
/// Motor de casamento de um instrumento. Prioridade preço-tempo: a melhor oferta do lado oposto
/// executa primeiro e, em empate de preço, a que chegou antes. O preço do negócio é sempre o da
/// ordem que já estava no livro. Não é thread-safe: a <see cref="Bolsa"/> serializa o acesso.
/// </summary>
public sealed class MotorDeCasamento
{
    private readonly IRelogio _relogio;
    private readonly Func<long> _proximoId;
    private readonly Func<long> _proximaSequencia;
    private readonly List<Evento> _eventos = new();

    public MotorDeCasamento(Instrumento instrumento, IRelogio relogio, Func<long> proximoId, Func<long> proximaSequencia)
    {
        Livro = new Livro(instrumento);
        _relogio = relogio;
        _proximoId = proximoId;
        _proximaSequencia = proximaSequencia;
    }

    public Livro Livro { get; }
    public Instrumento Instrumento => Livro.Instrumento;

    /// <summary>Quando verdadeiro, uma ordem que cruzaria com outra da mesma conta cancela a ordem que estava no livro em vez de negociar.</summary>
    public bool PrevenirAutoNegociacao { get; init; } = true;

    public Resultado Enviar(NovaOrdem pedido)
    {
        _eventos.Clear();
        var agora = _relogio.Agora;
        Ordem ordem;
        try
        {
            ordem = new Ordem(_proximoId(), pedido.IdCliente ?? string.Empty, pedido.Conta, Instrumento.Codigo, pedido.Lado, pedido.Tipo,
                pedido.Validade, pedido.Preco, pedido.Quantidade, agora, _proximaSequencia());
        }
        catch (ArgumentException e)
        {
            throw new OrdemInvalidaException(e.Message);
        }

        var motivo = Validar(ordem);
        if (motivo is not null)
        {
            ordem.Rejeitar(motivo);
            Publicar(new OrdemRejeitada(_proximaSequencia(), agora, ordem.Foto(), motivo));
            return new Resultado(ordem, _eventos.ToArray());
        }

        Publicar(new OrdemAceita(_proximaSequencia(), agora, ordem.Foto()));

        if (ordem.Validade == Validade.TudoOuNada && Livro.DisponivelPara(ordem) < ordem.QuantidadeRestante)
        {
            ordem.Cancelar("FOK sem liquidez suficiente");
            Publicar(new OrdemCancelada(_proximaSequencia(), agora, ordem.Foto(), ordem.MotivoEncerramento!));
            return new Resultado(ordem, _eventos.ToArray());
        }

        Casar(ordem, agora);

        if (ordem.QuantidadeRestante > 0)
        {
            if (ordem.Tipo == TipoOrdem.Limitada && ordem.Validade == Validade.AteCancelar)
            {
                Livro.Adicionar(ordem);
            }
            else
            {
                var razao = ordem.Tipo == TipoOrdem.Mercado ? "sem liquidez para o restante da ordem a mercado" : "IOC: restante cancelado";
                ordem.Cancelar(razao);
                Publicar(new OrdemCancelada(_proximaSequencia(), agora, ordem.Foto(), razao));
            }
        }
        return new Resultado(ordem, _eventos.ToArray());
    }

    public Resultado Cancelar(CancelarOrdem pedido)
    {
        _eventos.Clear();
        var ordem = Livro.Obter(pedido.OrdemId);
        if (ordem is null) return new Resultado(null, Array.Empty<Evento>());
        if (ordem.Conta != pedido.Conta) throw new ContaDiferenteException();
        Livro.Remover(ordem);
        ordem.Cancelar("cancelada pelo cliente");
        Publicar(new OrdemCancelada(_proximaSequencia(), _relogio.Agora, ordem.Foto(), ordem.MotivoEncerramento!));
        return new Resultado(ordem, _eventos.ToArray());
    }

    /// <summary>Altera preço e/ou quantidade. Se perder prioridade, a ordem é reinserida no fim da fila e pode cruzar imediatamente.</summary>
    public Resultado Alterar(AlterarOrdem pedido)
    {
        _eventos.Clear();
        var ordem = Livro.Obter(pedido.OrdemId);
        if (ordem is null) return new Resultado(null, Array.Empty<Evento>());
        if (ordem.Conta != pedido.Conta) throw new ContaDiferenteException();
        if (pedido.NovoPreco is { } p && !Instrumento.PrecoValido(p)) throw new OrdemInvalidaException($"preço fora do tick de {Instrumento.TickPreco}");
        if (pedido.NovaQuantidade is { } q && !Instrumento.QuantidadeValida(q)) throw new OrdemInvalidaException($"quantidade fora do lote de {Instrumento.LoteMinimo}");

        var agora = _relogio.Agora;
        var precoAnterior = ordem.Preco;
        var quantidadeAnterior = ordem.Quantidade;
        var perde = (pedido.NovoPreco is { } np && np != ordem.Preco) || (pedido.NovaQuantidade is { } nq && nq > ordem.Quantidade);

        if (!perde)
        {
            // só redução de quantidade: a ordem fica no mesmo lugar da fila, ajustando o agregado do nível
            try { ordem.Alterar(null, pedido.NovaQuantidade, ordem.Sequencia); }
            catch (InvalidOperationException e) { throw new OrdemInvalidaException(e.Message); }
            Livro.AjustarQuantidade(ordem, ordem.Quantidade - quantidadeAnterior);
            Publicar(new OrdemAlterada(_proximaSequencia(), agora, ordem.Foto(), precoAnterior, quantidadeAnterior, false));
            return new Resultado(ordem, _eventos.ToArray());
        }

        Livro.Remover(ordem);
        try
        {
            ordem.Alterar(pedido.NovoPreco, pedido.NovaQuantidade, _proximaSequencia());
        }
        catch (InvalidOperationException e)
        {
            Livro.Adicionar(ordem);
            throw new OrdemInvalidaException(e.Message);
        }
        Publicar(new OrdemAlterada(_proximaSequencia(), agora, ordem.Foto(), precoAnterior, quantidadeAnterior, true));
        Casar(ordem, agora);
        if (ordem.QuantidadeRestante > 0) Livro.Adicionar(ordem);
        return new Resultado(ordem, _eventos.ToArray());
    }

    private string? Validar(Ordem ordem)
    {
        if (ordem.Tipo == TipoOrdem.Limitada && !Instrumento.PrecoValido(ordem.Preco!.Value)) return $"preço fora do tick de {Instrumento.TickPreco}";
        if (!Instrumento.QuantidadeValida(ordem.Quantidade)) return $"quantidade fora do lote de {Instrumento.LoteMinimo}";
        return null;
    }

    private void Casar(Ordem agressora, DateTimeOffset agora)
    {
        while (agressora.QuantidadeRestante > 0)
        {
            var nivel = agressora.Lado == Lado.Compra ? Livro.MelhorVenda : Livro.MelhorCompra;
            if (nivel is null || !agressora.Cruza(nivel.Preco)) break;

            var passiva = nivel.Primeira!;
            if (PrevenirAutoNegociacao && passiva.Conta == agressora.Conta)
            {
                Livro.Remover(passiva);
                passiva.Cancelar("prevenção de auto-negociação");
                Publicar(new OrdemCancelada(_proximaSequencia(), agora, passiva.Foto(), passiva.MotivoEncerramento!));
                continue;
            }

            var quantidade = Math.Min(agressora.QuantidadeRestante, passiva.QuantidadeRestante);
            var preco = nivel.Preco;
            passiva.Executar(quantidade);
            agressora.Executar(quantidade);
            Livro.AposExecucao(passiva, quantidade);

            var (compra, venda) = agressora.Lado == Lado.Compra ? (agressora, passiva) : (passiva, agressora);
            var negocio = new Negocio(_proximoId(), Instrumento.Codigo, preco, quantidade, compra.Id, venda.Id, compra.Conta, venda.Conta, agressora.Lado, agora);
            Publicar(new NegocioExecutado(_proximaSequencia(), agora, negocio));
        }
    }

    private void Publicar(Evento evento) => _eventos.Add(evento);
}

/// <summary>Pedido malformado (preço, quantidade, tipo ou validade incompatíveis).</summary>
public sealed class OrdemInvalidaException : Exception
{
    public OrdemInvalidaException(string mensagem) : base(mensagem) { }
}

/// <summary>Tentativa de mexer em ordem de outra conta.</summary>
public sealed class ContaDiferenteException : Exception
{
    public ContaDiferenteException() : base("a ordem pertence a outra conta") { }
}
