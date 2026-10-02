# CLAUDE.md — Contrato de desenvolvimento

Este arquivo é o contrato de trabalho do Claude Code neste repositório. Leia-o inteiro antes de qualquer tarefa. Em caso de dúvida, este arquivo e os documentos listados em "Fontes de verdade" prevalecem sobre qualquer suposição.

## 1. Contexto do projeto

Jogo de futebol mobile focado **exclusivamente em modo carreira** de um clube brasileiro fictício. O jogador começa na Divisão D e busca levar o clube ao topo, jogando partidas em tempo real (ou simulando) e gerindo elenco, mercado, finanças, instalações e objetivos da diretoria. Inspiração estrutural em Dream League Soccer, com identidade própria. **Nenhum conteúdo protegido:** clubes, jogadores, escudos e competições são fictícios.

Nome de trabalho: **ACESSO** (PENDENTE: verificação de marca e disponibilidade).

## 2. Objetivo do jogo (resumo)

- Partida jogável responsiva, fácil de aprender e difícil de dominar, em que a qualidade do elenco é visível.
- Carreira longa (6–10 temporadas da D ao título da A), com decisões que importam.
- 100% offline, single-player, Android primeiro.

## 3. Stack tecnológica (decidida)

- Unity 6 LTS, C#, Android (ARM64, IL2CPP).
- URP, Input System (toque), UI Toolkit para menus.
- Núcleo em C# puro (sem Unity), compilado também por uma solution .NET paralela para `dotnet test`.
- Dados de balanceamento e definições em JSON versionado.
- Save em JSON + gzip, com migrações versionadas.
- Controle de versão: Git + Git LFS (D-06). Hospedagem (GitHub, GitLab etc.) não decidida e não necessária agora.

Não introduza tecnologia, pacote ou dependência fora desta lista sem decisão registrada em `DECISIONS.md`. JSON: Newtonsoft JSON (D-11). Item ainda PENDENTE (ver `DECISIONS.md`): uso do Cinemachine.

## 4. Fontes de verdade e hierarquia

| Documento | Autoridade |
| --- | --- |
| `TECHNICAL_SPEC.md` + `ARCHITECTURE.md` | Arquitetura, tecnologia, estrutura de código, sistemas, testes |
| `GAME_DESIGN.md` (seções da partida) | Experiência e design da partida |
| `GAME_DESIGN.md` (demais seções) | Visão do produto, carreira, identidade |
| `MVP_SCOPE.md` | O que entra e o que não entra no MVP |
| `ROADMAP.md` | Ordem das etapas e o que cada etapa pode conter |
| `DECISIONS.md` | Decisões tomadas e pendentes |
| `TEST_PLAN.md`, `DEFINITION_OF_DONE.md` | Quando algo está pronto |
| `REPOSITORY_STRUCTURE.md` | Onde cada coisa fica |

Conflitos: técnica → TECHNICAL_SPEC; partida → design da partida; visão/carreira/identidade → GAME_DESIGN. Se encontrar conflito não coberto: **pare, registre em `DECISIONS.md` como PENDENTE e pergunte.** Nunca resolva em silêncio.

## 5. Princípios arquiteturais

1. **Zona pura não conhece a Unity.** `Core`, `Data`, `Rules`, `Match`, `Match.AI`, `Simulation`, `Career`, `Career.Market`, `Career.Economy`, `Save` não referenciam `UnityEngine` (asmdef com `noEngineReferences: true`).
2. **Estado separado de apresentação.** A partida é simulação em passo fixo; câmera, animação, áudio e UI apenas leem estado e eventos.
3. **Uma entrada e uma saída por partida:** `MatchSetup` → `MatchResult`, para `MatchEngine` e `QuickSim`. Esses contratos pertencem ao `Core` (D-19); nem `Match` nem `Simulation` dependem um do outro por causa deles.
4. **Dados do jogo ≠ estado do save.** Definições vêm com o app; o save guarda apenas `CareerState`.
5. **Referências por Id inteiro** no estado persistido; nunca referência de objeto.
6. **A carreira inteira roda sem Unity** (com QuickSim) em teste de linha de comando por 10+ temporadas.

## 6. Regras de dependência

- Direção única: `Core ← Data ← Rules ← Match / Match.AI / Simulation ← Career (+Market, Economy) ← Save`.
- Assemblies Unity (`App`, `Input`, `Presentation`, `Audio`, `UI`, `Tools`) dependem das puras; nunca o inverso.
- `Match` nunca depende de `Career`.
- `Match` e `Simulation` não dependem um do outro; ambos usam os contratos `MatchSetup`/`MatchResult` do `Core`.
- Arquivos binários/pesados (modelos, texturas, áudio e similares) versionados via Git LFS.
- Nenhum pacote de terceiros na zona pura sem decisão registrada.

## 7. Regras de dados e balanceamento

- **Nenhum número de balanceamento no código.** Todo valor ajustável vive em JSON em `Data/` e é validado no carregamento.
- **`Data/` fica na raiz do repositório, fora de `Assets/`, e é a única fonte da verdade dos dados** (D-09). É lida pela Unity e pela solution .NET/headless. `StreamingAssets` nunca é fonte da verdade; se a build precisar de cópia dos dados, ela é gerada por etapa de build/empacotamento, sem edição manual e sem duplicar a fonte.
- **Atributos só via catálogo `Balance`:** `Balance.Eval(Effect.X, jogador)`. Nenhum sistema calcula efeito lendo atributo diretamente.
- Todo atributo deve alimentar ao menos um `Effect`; validação falha se não.
- ScriptableObjects não são fonte da verdade (os testes fora da Unity precisam dos mesmos dados).

## 8. Regras de RNG e determinismo

- Proibido na zona pura: `System.Random` compartilhado/global, `UnityEngine.Random`, `DateTime.Now`, `Time.deltaTime`, relógio real.
- RNG determinístico com **fluxos independentes por sistema**, derivados da semente da carreira/partida.
- Mesma semente + mesmas entradas = mesmo resultado no mesmo aparelho/build.
- Todo teste que falha deve registrar a semente usada.

## 9. Regras de testes

- Toda funcionalidade vem com testes executáveis por linha de comando (`dotnet test` na zona pura; Unity batchmode para PlayMode).
- Nenhuma etapa é concluída com testes quebrados, incluindo os de etapas anteriores.
- **Não afirme que algo funciona sem executar** os testes e, quando houver partida, o cenário headless correspondente.
- Ver `TEST_PLAN.md` e `DEFINITION_OF_DONE.md`.

## 10. Regras de performance

- Zero alocação por frame no loop da partida (sem LINQ, closures ou `new` no passo).
- Sem Rigidbody/Collider da Unity na partida.
- Simulação em passo fixo (valor inicial 50 Hz) desacoplada do render.
- Não otimizar sem medir, exceto os itens acima (que são regra desde o início).

## 11. Regras de escopo

- **Trabalhe somente na etapa atual do `ROADMAP.md`.** Não implemente nada de etapas futuras, mesmo que "seja rápido".
- Nada fora de `MVP_SCOPE.md` sem decisão registrada.
- Ideias e melhorias não pedidas: anote em `DECISIONS.md` como sugestão PENDENTE; não implemente.
- Não refatore código não relacionado à tarefa.

## 12. Decisões ambíguas

1. Consulte a hierarquia de fontes.
2. Se continuar ambíguo, **não escolha uma opção**: registre como PENDENTE em `DECISIONS.md`, explique as opções e pergunte.
3. Se a pendência bloquear a tarefa, pare e informe; se não bloquear, siga com o que não depende dela.

## 13. Definição de pronto

Uma funcionalidade só está pronta quando cumpre `DEFINITION_OF_DONE.md`. Compilar não é estar pronto.

## 14. Etapa atual

**Track B — B2 QuickSim** (em validação: `dotnet test` verde; falta a validação na Unity). B1 — Mundo e dados e Stage 0 — Foundation concluídas. Atualize esta seção quando a etapa mudar.
