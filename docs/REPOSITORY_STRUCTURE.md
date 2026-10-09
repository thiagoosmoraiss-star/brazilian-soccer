# REPOSITORY_STRUCTURE.md

Fonte: Especificação Técnica §16 + decisões D-06, D-09 e D-19. Nomes exatos de arquivos de projeto (.sln/.csproj) e de subpastas são definidos na Stage 0.

## Estrutura

```
<raiz>/
  CLAUDE.md
  docs/                      ARCHITECTURE, GAME_DESIGN, TECHNICAL_SPEC, ROADMAP, MVP_SCOPE,
                             DECISIONS, TEST_PLAN, DEFINITION_OF_DONE, REPOSITORY_STRUCTURE
  Data/                      JSON de balanceamento e definições — raiz do repositório, fora de Assets (D-09); fonte da verdade única
    Balance/                 effects, movement, ball, fatigue, quicksim, tactics...
    Formations/
    Competitions/
    World/                   pools de nomes, cidades fictícias, templates de clube/escudo
    Economy/, Market/, Board/, Facilities/, Staff/
    TestRanges/              faixas de aceitação dos testes estatísticos
  <ProjetoUnity>/
    Assets/_Game/
      Core/  Data/  Rules/  Match/  Match.AI/  Simulation/
      Career/  Career.Market/  Career.Economy/  Save/
      App/  Input/  Presentation/  Audio/  UI/
      Tools/Editor/
      Tests/Unit/  Tests/Simulation/  Tests/Career/  Tests/PlayMode/
      Content/               modelos, animações, materiais, UI assets, áudio
    Assets/Scenes/           Boot, Shell, Match, Dev/ (sandboxes)
  dotnet/                    solution .NET paralela (compila os fontes puros + testes puros)
```

Cada pasta em `Assets/_Game/` com código tem um Assembly Definition com o mesmo nome.

### Nomes definidos na Stage 0

- `<ProjetoUnity>` = `Unity/` (projeto Unity 6 LTS; `ProjectSettings/ProjectVersion.txt` = 6000.3.1f1).
- Solution .NET: `dotnet/Game.sln`, um `.csproj` por assembly pura (`dotnet/<Assembly>/<Assembly>.csproj`) + `dotnet/Tests.Unit/`.
- Namespaces: `Game.<Assembly>` (sem o nome de trabalho, pendente D-16).
- `Tests/Career/` (+ `dotnet/Tests.Career/`) criada na B1; `Tests/Simulation/` (+ `dotnet/Tests.Simulation/`) criada na B2 (testes lentos marcados com a categoria `Slow`).
- `dotnet/QuickSimReport/`: ferramenta de linha de comando da B2 que roda lotes da QuickSim e imprime as métricas de calibração.
- `dotnet/CareerSim/`: ferramenta de linha de comando da B3 que roda temporadas completas sem Unity (tabelas, acesso/rebaixamento, Copa).
- `dotnet/MatchReport/`: ferramenta de linha de comando da A7a — relatório headless do vertical slice (200 partidas forte × fraco + sensibilidade a Velocidade/Passe/Finalização). Uso: `dotnet run --project dotnet/MatchReport -c Release -- [--matches N] [--delta D]`.
- `Data/Presentation/match_view.json`: ajustes de apresentação da cena Match (câmera, radar, animação placeholder, HUD), lidos só pela Unity (A7b).
- `Unity/Assets/_Generated/` (ignorado pelo git): pacote de `Data/` gerado pelo passo de build para builds de aparelho (A7b, D-09).
- `Data/VerticalSlice/teams.json`: os dois times fictícios do vertical slice (forte ~70, fraco ~50); dados de demonstração/teste, fora do banco da carreira.
- Contratos `MatchSetup`/`MatchResult`/`MatchEvent` em `Core/Contracts/Match/` (D-19), criados na B2.
- `dotnet/WorldGen/`: ferramenta de linha de comando da B1 para gerar e inspecionar o mundo (fora de `Assets/`, não entra na build do jogo).

## Responsabilidades

| Área | Zona | Responsabilidade |
| --- | --- | --- |
| Core | Pura | Tipos base, Ids, RNG com fluxos, matemática (System.Numerics), eventos, log, resultado de operações; **contratos `MatchSetup`, `MatchResult`, `MatchEvent`** em área dedicada (D-19) |
| Data | Pura | GameDatabase, carregamento e validação de JSON, definições, catálogo `Balance` |
| Rules | Pura | Efeitos de atributos, falta/cartão/lesão, notas, força de time, OVR |
| Match | Pura | MatchEngine, MatchState, sistemas da partida, árbitro, bola parada, física |
| Match.AI | Pura | IA em 3 camadas, goleiro, seleção de controle |
| Simulation | Pura | QuickSim (usa os contratos do `Core`; não depende de `Match`) |
| Career | Pura | CareerState, calendário, competições, sistemas da carreira, gerador de mundo, serviços de aplicação |
| Career.Market | Pura | Valor, interesse, negociação, contratos, IA de mercado, scouting |
| Career.Economy | Pura | Livro-caixa, geradores de receita/despesa, orçamento |
| Save | Pura | Serialização, migrações, validação, backups, `IFileStore` (interface) |
| App | Unity | Boot, fluxo de cenas, ciclo de vida Android, implementação de `IFileStore` |
| Input | Unity | Toques → comandos da partida |
| Presentation | Unity | View da partida, câmera, animação, radar, VFX, vibração |
| Audio | Unity | Mixagem, eventos → sons |
| UI | Unity | Telas, presenters, HUD, strings |
| Tools/Editor | Unity Editor | Editor de curvas, visualizador de IA, simulador de temporadas, relatórios de calibração |
| Tests | Ambas | Unit, Simulation, Career (puros, `dotnet test`); PlayMode (Unity) |
| Content | Unity | Assets de arte e áudio (origem: PENDENTE D-02) |
| Data/ (JSON) | — | Fonte da verdade de todo balanceamento e definição; lida pela Unity e pela solution .NET/headless |
| dotnet/ | — | Compila as assemblies puras fora da Unity; falha se alguma usar Unity |

## Integração com a Unity

- Os fontes puros vivem uma única vez (em `Assets/_Game/...`); a solution `dotnet/` referencia esses mesmos arquivos (não há cópia).
- asmdefs puras com `noEngineReferences: true`.
- A Unity (App) carrega os JSON de `Data/`, na raiz do repositório, e entrega o GameDatabase às assemblies puras; os testes `dotnet` e o modo headless carregam os mesmos arquivos diretamente.
- `StreamingAssets` não é fonte da verdade. Se a build Android precisar dos dados dentro do app, uma etapa de build/empacotamento copia `Data/` para o local exigido; a cópia é gerada, nunca editada, e não é versionada como fonte.
- Versão de C#/.NET limitada à suportada pela Unity 6 (conferir na versão instalada).

## Controle de versão (D-06)

- Git + Git LFS. Serviço de hospedagem não decidido.
- Git LFS para arquivos binários/pesados que surgirem (ex.: modelos 3D, texturas, áudio, vídeos, fontes); regras de rastreamento definidas quando esses tipos entrarem no repositório.
- JSON de `Data/`, código e documentação ficam no Git normal (texto, diffs legíveis).
