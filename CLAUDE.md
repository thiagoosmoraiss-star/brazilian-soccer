# CLAUDE.md — Contrato de desenvolvimento

Este arquivo é o contrato de trabalho do Claude Code neste repositório. Leia-o inteiro antes de qualquer tarefa. Em caso de dúvida, este arquivo e os documentos listados em "Fontes de verdade" prevalecem sobre qualquer suposição.

## 1. Contexto do projeto

Jogo de futebol mobile focado **exclusivamente em modo carreira** de um clube brasileiro fictício. O jogador começa na Divisão D e busca levar o clube ao topo, jogando partidas em tempo real (ou simulando) e gerindo elenco, mercado, finanças, instalações e objetivos da diretoria. Inspiração estrutural em Dream League Soccer, com identidade própria. **Nenhum conteúdo protegido:** clubes, jogadores, escudos e competições são fictícios.

Nome do produto: **FOOTBALL NATION** (X-110, 19/10/2026). A verificação de marca continua pendente (D-16). O app, os menus de editor e o `productName` ainda mostram o nome de trabalho antigo, **ACESSO**, até a etapa F7a. Os identificadores técnicos internos ficam como estão. Requisitos do produto: `docs/PRODUCT_SPEC.md`.

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

Não introduza tecnologia, pacote ou dependência fora desta lista sem decisão registrada em `DECISIONS.md`. JSON: Newtonsoft JSON (D-11). Câmera de jogo: Cinemachine (D-10, X-48).

## 4. Fontes de verdade e hierarquia

| Documento | Autoridade |
| --- | --- |
| `PRODUCT_SPEC.md` (com `PRODUCT_BRIEF.md`) | **Produto:** o que o Football Nation é e o que entra na v1. Prevalece sobre `GAME_DESIGN.md` e `MVP_SCOPE.md` onde os contradiz (X-110) |
| `TECHNICAL_SPEC.md` + `ARCHITECTURE.md` | Arquitetura, tecnologia, estrutura de código, sistemas, testes |
| `GAME_DESIGN.md` (seções da partida) | Experiência e design da partida, no que o `PRODUCT_SPEC.md` não muda |
| `GAME_DESIGN.md` (demais seções) | Design-alvo de carreira, no que o `PRODUCT_SPEC.md` não muda. A identidade do §2 foi substituída |
| `MVP_SCOPE.md` | Itens do MVP antigo e a lista "fora", com as exceções de `PRODUCT_SPEC.md` §2.3 |
| `IMPLEMENTATION_PLAN.md` | Etapas F0–F8: escopo, critérios de conclusão, testes, validação manual |
| `ROADMAP.md` | Ordem das etapas e situação de cada uma |
| `DECISIONS.md` | Decisões tomadas e pendentes |
| `TEST_PLAN.md`, `DEFINITION_OF_DONE.md` | Quando algo está pronto |
| `REPOSITORY_STRUCTURE.md` | Onde cada coisa fica |

Conflitos:

- produto, escopo e identidade → PRODUCT_SPEC;
- técnica → TECHNICAL_SPEC;
- partida → design da partida;
- carreira não coberta pelo PRODUCT_SPEC → GAME_DESIGN. Se encontrar conflito não coberto: **pare, registre em `DECISIONS.md` como PENDENTE e pergunte.** Nunca resolva em silêncio.

## 5. Princípios arquiteturais

1. **Zona pura não conhece a Unity.** `Core`, `Data`, `Rules`, `Match`, `Match.AI`, `Simulation`, `Career`, `Career.Market`, `Career.Economy`, `Save`, `UI.Presenters` (X-96) não referenciam `UnityEngine` (asmdef com `noEngineReferences: true`).
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
- Nada fora do escopo da v1 (`PRODUCT_SPEC.md`) sem decisão registrada.
- Ideias e melhorias não pedidas: anote em `DECISIONS.md` como sugestão PENDENTE; não implemente.
- Não refatore código não relacionado à tarefa.

## 12. Decisões ambíguas

1. Consulte a hierarquia de fontes.
2. Se continuar ambíguo, **não escolha uma opção**: registre como PENDENTE em `DECISIONS.md`, explique as opções e pergunte.
3. Se a pendência bloquear a tarefa, pare e informe; se não bloquear, siga com o que não depende dela.

## 13. Definição de pronto

Uma funcionalidade só está pronta quando cumpre `DEFINITION_OF_DONE.md`. Compilar não é estar pronto.

## 14. Etapa atual

**19/10/2026 — Etapa 2 do Football Nation, só documentação (X-110).**

- Especificação oficial: `docs/PRODUCT_SPEC.md`.
- Plano das etapas F0–F8: `docs/IMPLEMENTATION_PLAN.md`, que **aguarda aprovação do usuário. Não implemente nenhuma etapa F antes disso.**
- Branch de trabalho: `integracao/c1p-lesao`. Ela contém a C3b e a lesão em campo (X-98).
- Na pasta de trabalho estão 2 itens preservados e não commitados (`Match.unity`, `RuntimeMaterialBuildStep.cs.meta`). Não os descarte: o destino deles é um item da F0.
- Progresso do roadmap: 72%, inalterado.

**Atualização da mesma data:** a F0 foi executada até onde é possível sem o usuário e sem aparelho (`docs/F0_2026-10-19.md`).

- A F0 **não está concluída**: faltam a validação no editor, a instalação nos aparelhos e as autorizações dos itens ★.
- As propostas D-22, D-23, D-24, D-25 e D-30 estão registradas em `DECISIONS.md`, mas **não autorizam implementação**.
- As decisões D-22, D-23a/b/c, D-24, D-25, D-30 e X-94b foram tomadas pelo usuário em X-111 (`DECISIONS.md`).

**Atualização:** a **F1 foi autorizada em X-112** (sem commits, push, integração na `main` nem F2). D-23d decidida em X-112: a dificuldade não tem efeito fora das partidas.

- **F1a** (X-113): dados, testes e relatório feitos. Falta a validação no editor.
- **F1b** (X-115 a X-119): **concluída** (X-119). D-36 (a), D-37 (b) e D-38 executadas (X-117); S-07 aceita como limitação conhecida (X-118); automatizados verdes (729/729 em 6 min, 131/131 lentos em 4 min), compilação e PlayMode na Unity ok, APK gerado; validação manual no aparelho e no editor relatada pelo usuário sem defeito impeditivo. **A F1c só começa com autorização explícita do usuário.**
- **F1c** (X-120, X-121): **concluída** (X-121). Dificuldade Fácil/Normal/Difícil (`Data/Balance/difficulty.json`; save v4); automatizados verdes (746/746), compilação na Unity ok; validação manual no aparelho aprovada pelo usuário. **A F2 só começa com autorização explícita do usuário** (plano da F2a apresentado, aguardando aprovação).
- **F2a** (X-122 a X-124, X-127): **implementada (X-127), não concluída: falta a validação manual no editor. Não comece a F2b.** D-32 resolvida. Gestão manual do clube do usuário (os outros com a IA); staff por escolha de nível, só com substituto válido (acima: soma dos custos de promoção; abaixo: sem taxa); obras de um nível, pagas no início, sem cancelar, uma por instalação; pagamentos validam o saldo atual (`Balance`); estádio abaixo do mínimo só gera aviso; um objetivo principal por temporada, avaliado no fim dela; patrocínio só para consulta; Copa no formato atual; save da carreira encerrada só para consulta (F2b). **Não implemente a F2a, a F2b nem melhorias gráficas sem autorização explícita** (direção visual proposta em `docs/VISUAL_DIRECTION.md`).
- **D-34 (X-114):** o lote de calibração fica em **6 min** (`calibration.json` `batch.durationMinutes`), com as tolerâncias intactas, até o fim da F1b. **Nunca afrouxe limites** para os testes passarem. A F1b só conclui com a calibração aprovada em **4 min**.
- Não comece a F1b antes de fechar a F1a e de apresentar o relatório ao usuário.

Situação anterior, mantida como histórico:

**Track C — C3 (UI) na sessão principal e o código de partida que ficou da C1/C2 numa sessão auxiliar, em paralelo (X-94).** Sessão principal (branch `claude/loving-brahmagupta-poh7zs`): **C3** — telas do MVP na Shell (`TECHNICAL_SPEC` §15), dividida em C3a–C3e (X-95); C3a validada (X-96); **C3b implementada (X-97)**, aguardando validação no editor; depois C3c (mercado). Sessão auxiliar (branch `claude/c1-partida`, X-100 a X-109): **X-91** (lesão em campo + troca forçada; fecha a C1) e **X-84 (a)** (ataque da IA); a principal faz a junção. C2 concluída (X-84 a X-87, juntada). C1 implementada e validada no editor (X-90); conclui com a X-91. Track A concluída (A1–A9). Track B concluída (B1–B8). Progresso do roadmap: 72% (`ROADMAP.md`, seção Progresso). Contexto geral do projeto: `docs/DOSSIE_ACESSO.html`. Atualize esta seção quando a etapa mudar.
