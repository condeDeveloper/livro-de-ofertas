# Livro de Ofertas

[![CI](https://github.com/condeDeveloper/livro-de-ofertas/actions/workflows/ci.yml/badge.svg)](https://github.com/condeDeveloper/livro-de-ofertas/actions/workflows/ci.yml)

Motor de casamento de ofertas (matching engine) em C# e .NET 8, do tipo usado no núcleo de uma bolsa: livro de ofertas com prioridade preço-tempo, ordens limitadas e a mercado, validades GTC/IOC/FOK, alteração com regra de perda de prioridade, prevenção de auto-negociação, candles, estatísticas de pregão e registro de eventos que permite reconstruir o estado do zero. Exposto por uma API REST com Swagger e por um hub SignalR em tempo real, com um simulador de fluxo para demonstração.

## Rodar

Só precisa do SDK do .NET 8.

```bash
dotnet run --project src/LivroDeOfertas.Api
```

- Documentação interativa: http://localhost:5000/docs
- Livro em tempo real: hub SignalR em `/hub/mercado` (métodos `Assinar(instrumento)` e `Cancelar`; mensagens `livro` e `negocio`)
- O simulador já começa a gerar ordens nos instrumentos fictícios `CONDE3`, `JOGO4` e `TESO11`. Desligue com `Simulador__Ativo=false`.

```bash
# enviar uma ordem limitada de compra
curl -s localhost:5000/api/ordens -H 'Content-Type: application/json' \
  -d '{"instrumento":"CONDE3","conta":"minha","lado":"Compra","tipo":"Limitada","quantidade":500,"preco":42.50}'

# foto do livro com 5 níveis
curl -s "localhost:5000/api/livro/CONDE3?niveis=5"

# candles de 1 minuto
curl -s "localhost:5000/api/candles/CONDE3?intervalo=1m"
```

## Testes e benchmark

```bash
dotnet test
dotnet run -c Release --project benchmarks/LivroDeOfertas.Benchmarks
```

Os testes cobrem as regras do motor (prioridade, preço da passiva, varredura, IOC, FOK, alteração, cancelamento, auto-negociação), um teste de propriedade com 5.000 ordens aleatórias que verifica que o livro nunca cruza e que a quantidade se conserva (executada + cancelada + no livro = enviada), a reconstrução a partir do registro de eventos, candles e estatísticas, e a API de ponta a ponta com `WebApplicationFactory`.

## Regras do motor

| Regra | Como funciona |
|-------|---------------|
| Prioridade | melhor preço primeiro; em empate, quem chegou antes |
| Preço do negócio | sempre o da ordem que já estava no livro |
| Limitada GTC | executa o que cruzar e o restante fica no livro |
| Limitada IOC | executa o que cruzar e cancela o restante |
| Limitada FOK | só executa se houver quantidade suficiente a preços aceitáveis; senão cancela inteira sem tocar o livro |
| A mercado | varre níveis até acabar a quantidade ou a liquidez; nunca fica no livro; só IOC ou FOK |
| Alteração | reduzir quantidade mantém a prioridade; mudar preço ou aumentar quantidade vai para o fim da fila e pode cruzar na hora |
| Auto-negociação | se a agressora encontra uma ordem da mesma conta, a ordem do livro é cancelada e o casamento continua |
| Tick e lote | preço fora do tick ou quantidade fora do lote são rejeitados antes de chegar ao livro |

## Arquitetura

```
src/LivroDeOfertas.Core        # domínio e motor, sem dependências
  Dominio/                     # Ordem, Instrumento, Negocio, NivelPreco, Livro, eventos
  Motor/                       # MotorDeCasamento (um instrumento), Bolsa (vários), registro de eventos
  Dados/                       # candles OHLCV e estatísticas de pregão
src/LivroDeOfertas.Api         # ASP.NET Core minimal API, SignalR, simulador
tests/LivroDeOfertas.Tests     # xUnit + FluentAssertions
benchmarks/LivroDeOfertas.Benchmarks   # BenchmarkDotNet
```

- O livro usa `SortedDictionary` por lado (compras decrescentes, vendas crescentes) com uma fila FIFO por nível e índice por id, o que dá O(log n) para inserir e O(1) para localizar e remover.
- Preços e quantidades são `decimal`: sem erro de ponto flutuante.
- Toda mudança de estado vira um evento imutável numa sequência global. `Bolsa.Reconstruir` reaplica os comandos e chega ao mesmo livro, com os mesmos ids.
- A `Bolsa` serializa o acesso por instrumento com um lock; o motor em si não tem sincronização, o que o mantém simples e rápido.

## Licença

MIT
