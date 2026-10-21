# IMPLEMENTATION_PLAN.md — Plano de implementação do Football Nation

Versão 1, 19/10/2026 (Etapa 2). **Proposta: aguarda aprovação do usuário. Nenhuma etapa deste plano foi iniciada.**

Fontes:

- Requisitos: `PRODUCT_SPEC.md`, com os ids FN-xx e as pendências D-xx.
- Estado do código: auditoria `Football Nation/docs/LEGACY_AUDIT.md` e integração `Football Nation/docs/INTEGRATION_2026-10-19.md`.
- Ordem anterior das etapas: `ROADMAP.md`.

Prioridade pedida pelo usuário:

1. Estabilizar a partida.
2. Carreira funcional.
3. Criação de clube.
4. Treinamento.
5. Mercado.
6. Personalização.
7. Apresentação visual.

Princípios:

- **Não reconstruir.** Tudo parte de `integracao/c1p-lesao` (LEGACY_AUDIT §6).
- As regras do `CLAUDE.md` continuam valendo para todas as etapas:
  - zona pura sem Unity;
  - balanceamento em JSON;
  - RNG por fluxo;
  - zero alocação no passo;
  - versão de schema e migração a cada mudança de save;
  - `DEFINITION_OF_DONE.md`.
- **Etapas pequenas, cada uma com um critério que se verifica executando algo.** Uma etapa só conta como concluída depois da validação manual descrita nela, quando houver uma.
- **Nenhuma etapa começa com pendência bloqueante em aberto.** As pendências estão listadas em cada etapa.

---

## 1. Estado real do projeto (19/10/2026)

### Repositório

| Item | Estado |
| --- | --- |
| Branch de trabalho | `integracao/c1p-lesao` @ `6c227af` = C3b (`ec6401c`) + lesão em campo (`b3064db`), publicada no GitHub |
| Linha principal antiga | `claude/loving-brahmagupta-poh7zs` @ `ec6401c`. `main` @ `640ec30`, muito atrás |
| Pasta de trabalho | 2 itens preservados e não commitados: `Unity/Assets/Scenes/Match.unity` (só renumeração de fileIDs) e `RuntimeMaterialBuildStep.cs.meta` (nunca versionado) |
| Backups | Branch local `backup/wip-2026-10-19` (`9e96ce0`), branch remota `backup/c1-partida-b3064db` e o bundle em `Football Nation/backups/2026-10-19_pre-integracao/` |
| Worktrees `c1p`/`c2` | Ponteiros quebrados (`prunable`), não reparados |
| Unity Hub | Ainda aponta para o caminho antigo `Documents/Thiago/brazilian-soccer/Unity` |
| Push | Precisa de `-c credential.https://github.com.username=thiagoosmoraiss-star` (o usuário padrão do git é outra conta) |

### Evidências

Testes executados em 19/10/2026 sobre o código de `4371737`:

- `dotnet build`: 0 avisos e 0 erros.
- `dotnet test` completo, com a categoria Slow: **709/709**.
- Unity 6000.3.1f1 em batchmode: 0 erros de compilação; PlayMode com 1 aprovado e 1 ignorado.

**Não verificado:**

- Build Android do commit atual.
- Execução em aparelho.
- A lesão em campo e a C3b no editor.
- Os testes no runtime .NET 8: só o .NET 10 está instalado, e os testes rodaram com `DOTNET_ROLL_FORWARD=Major`.

### Situação por funcionalidade

Legenda:

- **T:** implementado e testado (automatizado + validado pelo usuário).
- **NV:** implementado e testado automaticamente, não validado manualmente.
- **P:** parcial.
- **A:** ausente.
- **B:** bloqueado por decisão pendente.

| Área | Funcionalidade | Situação | Evidência / lacuna |
| --- | --- | --- | --- |
| Partida | Física da bola, movimento, posse, passe, chute, drible, defesa, goleiro, árbitro, bolas paradas, táticas, substituições, sensibilidade dos 18 atributos | **T** | A1–A9, centenas de testes, validação em aparelho e editor (X-63, X-74, X-76, X-82) |
| Partida | Lesão em campo e troca forçada | **NV** | X-100, `InjuryTests`; sem validação no editor |
| Partida | Placar crível (2–4 gols) | **P** | ~0,7 gol e ~50% de 0×0 em 6 min (X-84) |
| Partida | Duração 3/4/5 | **P / B** | Existe com 4/6/10; D-22 |
| Partida | Dificuldade | **A / B** | Nível único; D-23 |
| Partida | Câmera broadcast e HUD | **P** | Funcionam no aparelho; sem teste automatizado; textos e cores no código |
| Partida | Controles personalizáveis | **A** | Layout fixo; D-26 só para o mapeamento |
| Carreira | Mundo, QuickSim, calendário, 4 séries, sobe/desce, Copa, evolução passiva, economia, diretoria, instalações, staff, save | **T** (regras) | B1–B8, C2; testes de 10+ temporadas |
| Carreira | Integração carreira ↔ partida jogada | **T** | X-90 |
| UI | Shell, inicial, escolha de clube, Hub, pré e pós-jogo, competições, calendário, configurações | **T** | C3a, X-96 |
| UI | Elenco, perfil, escalação e tática do usuário | **NV** | C3b, X-97 |
| UI | Clube, diretoria e finanças com comandos do usuário | **A** | Tela de espera; regras automáticas |
| UI | Mercado e negociação com comandos do usuário | **A** | Tela de espera; X-95 |
| Carreira | Criar clube / editar clubes | **A / B** | D-24 |
| Carreira | Treinamento ativo | **A / B** | D-25 |
| Carreira | Mercado só brasileiro | **P / B** | Venda ao exterior existe; D-30 |
| Competições | Nomes "Série A–D" | **A** | Dado `competitions.json` + strings |
| Competições | Formato além de pontos corridos | **B** | D-31 |
| Save | Robustez e recuperação | **T** (regras) | B8; falta o aviso na UI quando um backup é restaurado |
| Identidade | Nome FOOTBALL NATION, paleta e tema único | **A** | "ACESSO" e cores fixas em 16 arquivos |
| Apresentação | Modelos, animações, estádio e iluminação realistas | **A / B** | Só primitivas; D-02 |
| Apresentação | Níveis gráficos | **A / B** | D-27 |
| Apresentação | Áudio | **A** | Assembly `Audio` vazio (C4) |
| Plataforma | Build Android, empacotamento de `Data/` | **P** | Configurada; última build em 14/10 |

---

## 2. Dependências entre funcionalidades

```
F0 base/validações ──► F1 partida ──┬─► F1c dificuldade (precisa de F1a + F1b: níveis afinados sobre a meta de gols)
                                    └─► F6 controles (HUD/Input estáveis)
F0 ──► F2 carreira funcional ──┬─► F3 criar/editar clube (Shell + save)
                               ├─► F4 treinamento (F2a: CT/auxiliar na UI; energia/lesão)
                               └─► F5 mercado (F2a: finanças/orçamento na UI)
F3, F4, F5 mudam o save → executar em sequência (um schemaVersion por vez)
F7a identidade/tema ─ independente (só conflita com F1/F6 em MatchHudView: fazer depois delas ou combinar)
F7b arte realista ──► F7c níveis gráficos ──► F7e medição em aparelho ──► F8 polimento/loja
D-02 ──► F7b;  D-27 ──► F7c;  D-29 ──► ícone final
```

| Funcionalidade | Depende de | Por quê |
| --- | --- | --- |
| Duração 3/4/5 (F1a) | D-22 | A calibração (`calibration.json`) e as metas de gols são de 6 min |
| Ataque da IA (F1b) | F1a | A meta de gols por partida depende da duração |
| Dificuldade (F1c) | F1a, F1b, D-23 | Afinar níveis sobre uma IA que ainda não ataca distorce todos os níveis. A calibração com a QuickSim precisa ficar no nível de referência |
| Controles (F6) | F1 | O HUD e o `InputAdapter` mudam em F1c (seleção de dificuldade) e no aviso de lesão |
| Clube, diretoria e finanças (F2a) | C3a/C3b | Mesma Shell e mesmos presenters |
| Criar clube (F3) | F2, D-24 | Fluxo de nova carreira; o save muda |
| Treinamento (F4) | F2a, D-25 | O CT e o auxiliar entram na UI em F2a; o treino mexe em energia e lesão (B4, X-100) |
| Mercado (F5) | F2a, D-30, S-11 | O orçamento e as finanças precisam estar visíveis; regra do exterior |
| Tema (F7a) | D-28 (parcial) | As cores de estado são necessárias para trocar o `UiKit` sem inventar valores |
| Arte realista (F7b) | D-02 | Origem, licença e esqueleto definem o pipeline |
| Níveis gráficos (F7c) | F7b, D-27 | Só faz sentido medir com a arte real |

---

## 3. Etapas

Cada etapa traz:

- **Objetivo.**
- **Pré-requisitos.**
- **Escopo.**
- **Critérios de conclusão (objetivos).**
- **Testes automatizados.**
- **Validação manual na Unity / aparelho.**
- **Não implementar.**

**Regras comuns a todas as etapas:**

- `dotnet build` com 0 avisos.
- `dotnet test` completo verde, **incluindo Slow**, antes de concluir.
- Unity batchmode sem erro de compilação e PlayMode verde.
- `DECISIONS.md` e `CLAUDE.md` §14 atualizados.

### F0 — Base do repositório e validações pendentes (sem funcionalidade nova)

> **Execução em 19/10/2026:** relatório em `F0_2026-10-19.md`, com a situação de cada critério abaixo e o procedimento seguro para as worktrees.
>
> **Pré-requisitos da F1, atualizados em 19/10/2026 (X-111):**
>
> - D-22, D-22b, D-23, D-23a, D-23b, D-23c e X-94b foram decididas pelo usuário.
> - **Nenhuma decisão bloqueia mais a F1**; ela só espera a **autorização do usuário**.
> - D-23d decidida em X-112: a dificuldade afeta só as partidas jogadas e simuladas, sem efeito na carreira.
> - **F1 autorizada pelo usuário em X-112** (sem commits, push nem F2).
>
> Como as decisões entram na F1:
>
> - **F1a:** a duração padrão passa a 4 min; 6 e 10 min continuam como opção, sem migração de configuração. As metas de `PRODUCT_SPEC.md` §4.1 vão para `Data/TestRanges/` como **referência experimental**.
> - **F1b:** uma linha de trabalho só. As metas servem de alvo de ajuste, não de critério rígido. O critério de conclusão passa a ser: "gols e finalizações na direção das metas experimentais, calibração verde **em 4 min com as tolerâncias existentes** (D-34), faixas registradas em `DECISIONS.md`".
> - **D-34 (X-114):** durante a F1b, o lote de calibração fica em 6 min com as tolerâncias intactas; as 9 falhas em 4 min são diagnóstico conhecido. Nenhum limite é afrouxado. No fim da F1b o lote passa a 4 min e tem de passar.
> - **F1c:**
>   - Fácil, Normal e Difícil, com o Normal recalibrado depois da F1b.
>   - Na partida simulada, o modificador vale só no confronto do clube do usuário.
>   - Novo teste: os outros confrontos e o lote de calibração ficam bit a bit iguais, com a mesma semente.
>   - O nível é alterável em Configurações e gravado no save (versão de schema e migração).

- **Objetivo:** fechar o que ficou aberto antes de mexer no jogo.
- **Pré-requisitos:** aprovação do usuário para cada item marcado (★).
- **Escopo:**
  1. ★ Validação, pelo usuário, da **lesão em campo** (X-100) e da **C3b** (X-97) no editor. Roteiro na tabela abaixo.
  2. ★ Destino de `Match.unity` e do `.meta`. Recomendação: commitar o `.meta`, para a Unity não gerar outro guid, e descartar a renumeração da cena (o conteúdo é idêntico). O backup já existe.
  3. ★ Levar `integracao/c1p-lesao` para `claude/loving-brahmagupta-poh7zs` com fast-forward, e decidir se a `main` passa a acompanhar o trabalho.
  4. ★ Reparar as worktrees (`git worktree repair`) ou removê-las. Atualizar o caminho no Unity Hub.
  5. ★ D-33: instalar o runtime .NET 8 ou migrar os testes para `net10.0`.
  6. ★ Credencial do git: corrigir a conta padrão ou manter o override por chamada.
  7. Build Android de desenvolvimento do commit atual e medição de FPS no A07 e no S10+, para ter uma **linha de base** antes das mudanças.
  8. Conferir o teste `Player_ReportsDataNotPackaged_UntilPackagingExists` numa build de player (LEGACY_AUDIT §5 item 6).
  9. ★ Definir quem executa a X-84 (a): a sessão auxiliar (X-94) ou a principal. Não há commit da auxiliar depois de `b3064db`.
- **Critérios de conclusão:**
  - `git status` sem itens não intencionais.
  - Uma linha principal definida.
  - O APK instalado nos 2 aparelhos, com FPS registrado em `DECISIONS.md`.
  - A validação do usuário registrada. Com ela, a C1 conta no progresso (+5%) e a C3b fica validada.
- **Testes:** `dotnet test` completo (no .NET 8, se D-33 for por instalar) e PlayMode.
- **Validação manual:**

  | Roteiro | O que fazer | Resultado esperado |
  | --- | --- | --- |
  | Lesão em campo (X-100) | Na carreira, jogar até ocorrer uma lesão. Partidas de 10 min aumentam a chance | Jogo pausa, aviso aparece, o usuário escolhe o substituto; sem trocas restantes o time fica com 10; a lesão aparece no elenco depois |
  | Tática e escalação (C3b) | Montar escalação e tática no elenco | O pré-jogo e a partida usam o que foi escolhido. Com Simular também |
  | Perfil do jogador (C3b) | Abrir o perfil | Mostra a faixa de potencial |
- **Não implementar:** nenhuma regra, tela ou dado novo.

### F1 — Estabilização da partida

#### F1a — Duração 3/4/5 min

- **Pré-requisitos:** F0; **D-22**.
- **Escopo:**
  - `durationOptions` e `durationMinutes` em `competitions.json`.
  - Passar a calibração (`Data/TestRanges/calibration.json`, X-86) para a duração padrão. **Mudado por D-34 (X-114):** o lote fica em 6 min até o fim da F1b; a passagem a 4 min é critério da F1b.
  - Revisar as faixas de teste que supõem 6 min.
  - A fadiga já é normalizada (X-12).
  - Migração de configurações salvas com 6 ou 10 min.
- **Critérios de conclusão:**
  - As 3 durações aparecem em Configurações.
  - A partida jogada termina no tempo real configurado.
  - A fadiga total é igual nas 3 durações (teste existente estendido).
  - A calibração está verde no lote de 6 min, com as tolerâncias de antes (D-34). Em 4 min, as 9 falhas ficam registradas (X-113).
  - Um save com 6 ou 10 min abre e cai na duração definida por D-22.
- **Testes:**
  - `FatigueTests` para 3/4/5.
  - `CalibrationTests` na duração padrão.
  - Teste de migração das configurações.
  - `ShellFlowTests` com as opções.
- **Validação manual:** jogar uma partida de cada duração no editor e conferir o relógio e o ritmo.
- **Não implementar:** a meta de gols (é a F1b) nem a dificuldade.

#### F1b — Ataque da IA (X-84 (a))

> **Execução em 19/10/2026 (X-115):** implementada e **não concluída**. Feito: valor de posse, atacar a área, corredor e alcance de chute, faltas na nova duração, correção da perseguição fora do campo. Em 4 min: 0,68 gol, 3,56 finalizações, 50% de 0×0. Calibração em 6 min verde. Em 4 min, falha só a subida da curva de OVR (ruído; **D-35**). Falta a validação no aparelho.
>
> **Continuação (X-116, D-35 (c)):** lote de calibração 1 200 / 12 000 (tolerâncias intactas), bloqueio de chute. Testes oficiais verdes em 6 e em 4 min; por semente, 4 de 6 em cada duração. Abertos: D-36, D-37, D-38.
>
> **Decisões D-36 (a), D-37 (b), D-38 (X-117):** erro de domínio, saída longa sob pressão e atacantes de área por mentalidade, faltas 3,0; correção do impasse de posse. Calibração 6 de 6 sementes em 4 e em 6 min; suíte 729/729 (6 min), lentos 131/131 (4 min). S-07 parcial. **Falta a validação no aparelho** (inclui o domínio do jogador do usuário).
>
> **Validação final (X-118):** S-07 aceita como limitação conhecida. Suíte 729/729 (6 min) e lentos 131/131 (4 min, restaurado); Unity: compilação sem erros, PlayMode 1 aprovado e 1 só de player; APK de desenvolvimento gerado. **Pendente:** 5 partidas no Android (sem aparelho conectado) e as 5 durações no editor.
>
> **Concluída (X-119, 20/10/2026):** validação manual feita pelo usuário no Galaxy S25 Ultra e no editor, sem defeito impeditivo (relato do usuário; log do aparelho parcial e sem erros do jogo).

- **Pré-requisitos:** F1a; definir quem executa (F0 item 9).
- **Escopo:**
  - Progressão, apoio no último terço, cruzamentos e escanteios em `Match.AI`, com parâmetros em `ai.json`.
  - Apertar as tolerâncias de volume da calibração.
  - Reavaliar S-07.
- **Critérios de conclusão:**
  - Em 400 partidas headless na duração padrão, a média de gols fica dentro da meta de D-22 (proporcional a 2–4 em 6 min, se D-22 decidir assim).
  - O % de 0×0 fica dentro da faixa em dados.
  - A calibração QuickSim × jogada fica verde **em 4 min** (lote de `calibration.json` de volta à duração padrão), com as tolerâncias existentes ou mais apertadas, nunca mais frouxas (D-34).
  - A sensibilidade dos 18 atributos continua verde.
- **Testes:**
  - `CalibrationTests`.
  - `SensitivityTests`.
  - `AiMatchTests` (sem oscilação, sem "bolo", zero alocação).
  - Relatório `dotnet/MatchReport --calibration` anexado à decisão.
- **Validação manual:** o usuário joga 5 partidas no aparelho e confirma placares críveis e IA que chega à área.
- **Não implementar:** dificuldade, mando de campo (X-85 (b)), fintas ou novos tipos de passe.

#### F1c — Dificuldade da partida (e da carreira, se D-23 incluir)

> **Execução em 20/10/2026 (X-120):** implementada e **não concluída**. `Data/Balance/difficulty.json` (Fácil, Normal, Difícil) age só sobre o adversário do usuário: decisão, reação, posicionamento e goleiro na partida; força por setor na QuickSim do confronto do usuário. Normal idêntico à IA atual com a mesma semente. Em 4 min o usuário vence 37% / 22% / 14,5%; z pareado 5,3 e 4,3 (mínimo 3). Escolha na nova carreira e em Configurações, save v4. Suíte 746/746; Unity compila; APK gerado. **Falta a validação no aparelho.**
>
> **Concluída (X-121, 20/10/2026):** validação manual no aparelho aprovada pelo usuário: diferenças perceptíveis entre os níveis, o Difícil com desafio elevado, o restante como esperado.

- **Pré-requisitos:** F1b; **D-23**.
- **Escopo:**
  - Níveis em dados, por exemplo `Data/Balance/difficulty.json`, aplicados aos parâmetros de decisão e reação da IA (`ai.json`, `goalkeeper.json`, `defense.json`).
  - Regra do GAME_DESIGN §24: **não altera atributos**, salvo se D-23 decidir outra coisa.
  - Seleção do nível na nova carreira e/ou em Configurações, conforme D-23.
  - O nível fica gravado no save (com versão de schema e migração) ou nas configurações.
- **Critérios de conclusão:**
  - Em lotes headless, com o mesmo confronto e o mesmo jogador simulado, o adversário da IA vence mais quanto maior o nível. A diferença entre níveis vizinhos é estatisticamente significativa (faixas em `Data/TestRanges/`).
  - O nível de referência é idêntico ao comportamento atual: a calibração não muda.
  - A QuickSim só muda se D-23 decidir.
- **Testes:**
  - Teste de monotonicidade por nível.
  - Teste de que o nível de referência reproduz a IA atual com a mesma semente.
  - Migração do save.
  - Teste do presenter de Configurações / nova carreira.
- **Validação manual:** o usuário joga pelo menos 2 partidas no nível mais fácil e 2 no mais difícil, no aparelho, e confirma que a diferença se sente.
- **Não implementar:** ajuste dinâmico durante a partida (rubber-banding é proibido pelo GAME_DESIGN §15).

### F2 — Carreira funcional

#### F2a — Clube, diretoria e finanças (ex-C3d)

> **Decisões do usuário (X-122, 20/10/2026):** gestão manual do clube do usuário (a IA não decide obras, contratações nem demissões por ele; os outros clubes seguem com a IA); demitir staff exige substituto válido, sem vaga vazia persistida; a D-32 é resolvida antes da F2a (proposta D-32a a D-32f em `DECISIONS.md`, aguardando aprovação). **Não iniciada.**
>
> **X-123:** confirmados avaliação da diretoria no fim da temporada, requisito de estádio só como aviso, staff por escolha de nível (nível abaixo sem taxa), Copa no formato atual (fase de março verificada sem conflito), escalação e tática automáticas como padrão. **Ainda bloqueiam a F2a:** taxa para staff de nível acima, critério de caixa dos comandos, regras do comando de obra e o restante da D-32a.
>
> **X-124:** aprovados taxa de staff acima = soma dos custos de promoção; operações pagas validam o saldo atual (`Balance`); obra de um nível por vez, paga no início, sem cancelar, uma por instalação; um objetivo principal por temporada, patrocínio fixo só para consulta, sem objetivos secundários. **A D-32 está resolvida para a F2a; não há outra decisão bloqueante. A F2a continua não iniciada e depende de autorização explícita.**
>
> **Execução (X-127, 20/10/2026):** implementada e **não concluída**. Comandos de obra e de staff, gestão automática desligada só no clube gerido (carreiras só de IA idênticas byte a byte), validador de staff, telas Diretoria, Clube e Finanças. Suíte 762/762; Unity compila; APK gerado. **Falta a validação manual no editor.**

- **Pré-requisitos:** F0, F1; **D-32 resolvida** (D-32a, D-32b, D-32c e D-32f aprovadas; X-122 (3)): **cumprido em X-124**.
- **Gestão manual (X-122):** as rotinas mensais de obra e de staff pulam o clube gerido; conclusão de obra, salários, manutenção e receitas continuam como regras. Comandos do usuário: iniciar melhoria de instalação (um nível, pago no início, sem cancelar, uma obra por instalação) e substituir um membro do staff (atômico; nível acima paga a soma dos custos de promoção, nível abaixo sem taxa). Pagamentos validam o saldo atual (`Balance`) e passam pelo livro-caixa (X-124).
- **Escopo:**
  - Comandos do usuário no código puro: mandar construir (estádio, CT), contratar e demitir staff, e ver o orçamento.
  - Telas Clube, Diretoria e Finanças (TECHNICAL_SPEC §15).
  - Textos em `pt-BR.json`.
- **Critérios de conclusão:**
  - As telas de espera de Diretoria somem.
  - Cada comando valida a regra (caixa, requisito, limite) e tem teste.
  - Saldo = soma do livro-caixa depois dos comandos.
  - 10 temporadas automáticas continuam estáveis.
  - Uma temporada com clube gerido e sem comandos não cria obra nem muda o staff do clube gerido; os clubes da IA seguem como antes (X-122).
  - Uma carreira só de IA é idêntica à anterior à F2a com a mesma semente.
  - Nunca há vaga de staff vazia persistida: o validador do save exige um membro por função no clube gerido.
- **Testes:** `Tests.Career` (comandos e invariantes), `Tests.UI` (presenters, strings), `Tests.Save` (se o formato mudar).
- **Validação manual:** no editor, fazer uma obra, contratar um auxiliar e conferir finanças e diretoria ao longo de uma temporada.
- **Não implementar:** loja, ingresso ajustável, empréstimo, departamento médico.

#### F2b — Ciclo de temporada e fim de carreira pela UI

- **Pré-requisitos:** F2a.
- **Escopo:**
  - Telas e fluxo de fim de temporada: acesso, rebaixamento, objetivos e balanço.
  - Demissão e fim de carreira, levando a uma nova carreira. Só na avaliação de fim de temporada; o save da carreira encerrada fica **só para consulta, sem retomar** (D-32e, X-123). A decidir antes da F2b: o que acontece com ela quando uma nova carreira é criada (slot único de save).
  - Aviso quando o save é restaurado de backup (FN-14).
  - Migração dos textos da partida (HUD, pausa, táticas, substituições) para `pt-BR.json` (ex-C3e).
  - PlayMode de navegação.
  - Alvos de toque ≥ 44 pt.
  - Do fim da partida à próxima partida em ≤ 3 toques.
- **Critérios de conclusão:**
  - Uma carreira de 2 temporadas completa pela UI no editor: jogar, simular, transição, ver o acesso.
  - O teste "≤ 3 toques" está verde.
  - Nenhum texto visível fica fora da tabela de strings: um teste falha se ficar.
  - A corrupção simulada do save mostra o aviso e continua a carreira.
- **Testes:** PlayMode de navegação (novo), `Tests.UI`, `Tests.Save` (corrupção → aviso).
- **Validação manual:** o usuário joga 1 temporada inteira no aparelho (algumas partidas jogadas e as outras simuladas).
- **Não implementar:** mercado do usuário (F5), identidade nova (F7a).

### F3 — Criar clube e clubes editáveis

- **Pré-requisitos:** F2; **D-24**.
- **Escopo:**
  - Comando puro `CreateClub` (nome, sigla, cores, escudo por template, UF e o resto que D-24 definir).
  - Elenco inicial gerado pelas regras do `WorldGenerator`.
  - Edição de clubes conforme D-24.
  - Telas de criação e edição.
  - Versão de schema do save e migração.
- **Critérios de conclusão:**
  - Uma carreira criada com clube próprio roda 10 temporadas sem Unity, com os invariantes: 16 por série, Ids únicos, elenco de 20–26.
  - A mesma semente e as mesmas escolhas dão o mesmo mundo.
  - Saves anteriores abrem.
  - Nenhum escudo reproduz um real, porque só usa templates (X-02).
- **Testes:** `WorldGeneratorTests` (clube criado), `SeasonTests` (10 temporadas), `Tests.Save` (migração e fixture), `Tests.UI`.
- **Validação manual:** criar o clube no aparelho, conferir as cores na partida e no escudo, e jogar a 1ª partida.
- **Não implementar:** editor de jogadores (a não ser que D-24 inclua), uniformes detalhados, upload de imagem.

### F4 — Treinamento

- **Pré-requisitos:** F2a; **D-25**.
- **Escopo:**
  - Sistema puro de treino em `Career`, com dados em `Data/Career/training.json`.
  - Plano conforme D-25, com intensidade, frequência e foco.
  - Liga o treino à evolução semanal (`PlayerSystems`), à energia e à lesão.
  - O potencial continua sendo o teto.
  - A política automática da IA para os outros clubes.
  - A tela de treino.
  - Save versionado.
- **Critérios de conclusão:**
  - Em lotes de 10 temporadas, com o mesmo elenco, um treino de foco X faz os atributos de X subirem mais que sem foco (estatisticamente significativo).
  - Ninguém passa do potencial.
  - O OVR médio por série continua nas faixas do GAME_DESIGN §12.
  - Intensidade alta aumenta a fadiga e o risco de lesão na direção esperada.
  - O tempo D→A em simulação continua entre 6 e 10 temporadas.
- **Testes:** `Tests.Career` (novos: treino, limites, invariantes), `PlayerDevelopmentTests` (regressão), `Tests.Save`, `Tests.UI`.
- **Validação manual:** o usuário define um plano, avança 2 meses e vê a mudança de atributos e de OVR no perfil.
- **Não implementar:** sessões de treino jogáveis, minijogos, departamento médico.

### F5 — Mercado do usuário (ex-C3c)

- **Pré-requisitos:** F2a; **D-30**; S-11.
- **Escopo:**
  - Comandos do usuário no código puro: busca com filtros, proposta ao clube, contraproposta, proposta ao jogador (salário, anos), aceitar e recusar propostas recebidas, renovar, agentes livres e pré-contrato.
  - Os números do scouting passam para `market.json` e o fator do olheiro passa a ser aplicado (S-11).
  - Regra do mercado só brasileiro, conforme D-30.
  - Telas Mercado e Negociação.
- **Critérios de conclusão:**
  - Uma negociação completa pela UI atualiza o elenco, o caixa e a folha de forma consistente: saldo = soma do livro, e o jogador fica com um só contrato.
  - Os elencos ficam entre 20 e 26.
  - A inflação fica dentro do limite em 10 temporadas.
  - Nenhuma transferência para clube fora do Brasil, se D-30 decidir assim (teste de invariante).
- **Testes:** `MarketTests` (comandos do usuário), invariantes de economia, `Tests.UI`, `Tests.Save`.
- **Validação manual:** no aparelho, contratar 1 jogador, vender 1 e renovar 1 numa janela.
- **Não implementar:** empréstimos, cláusulas, parcelas, trocas, rede de olheiros.

### F6 — Personalização de controles e configurações

- **Pré-requisitos:** F1. A D-26 só bloqueia o mapeamento.
- **Escopo:**
  - Editor de layout: arrastar e redimensionar o analógico e os botões, com limites de tamanho e de área segura.
  - Sensibilidade do analógico.
  - Mapeamento conforme D-26.
  - Restaurar o padrão.
  - Persistência em configurações do aparelho, separada da carreira, com versão.
  - Modelo do layout em código puro (testável).
- **Critérios de conclusão:**
  - O layout salvo é restaurado depois de reiniciar o app.
  - Os botões nunca saem da área segura nem ficam menores que 44 pt.
  - O layout é proporcional em 3 resoluções/razões de aspecto (16:9, 20:9 e tablet 4:3).
  - O padrão restaura o layout original.
  - Zero alocação no passo continua.
- **Testes:**
  - Modelo do layout: limites, colisão entre botões, escala por resolução.
  - Persistência: ida e volta, arquivo corrompido volta ao padrão.
  - `ControlSwitchTests` e `InputIntentFilterTests` (regressão).
- **Validação manual:** no A07 e no S10+, mover e redimensionar os botões, jogar uma partida e reiniciar o app.
- **Não implementar:** gestos novos, vibração ou modo canhoto, a não ser que D-26 inclua.

### F7 — Apresentação visual

#### F7a — Nome, tema e identidade

- **Pré-requisitos:** D-28 para as cores de estado. O nome não depende de nada; a marca (D-16) só bloqueia a loja.
- **Escopo:**
  - Tema único em dados (por exemplo `Data/Presentation/theme.json`) com a paleta do brief.
  - `UiKit`, `MatchHudView`, `SubstitutionPanel` e `MatchBootstrap` passam a ler o tema.
  - `app.title` = FOOTBALL NATION.
  - `productName` e menus de editor.
  - "Série A–D" em `competitions.json` e nas strings.
  - Ícone provisório.
  - Os identificadores internos ficam como estão.
- **Critérios de conclusão:**
  - Um teste falha se houver literal de cor fora do tema em `UI`/`Presentation` (varredura de código).
  - Nenhuma ocorrência visível de "ACESSO" no app (varredura das strings e do `ProjectSettings`).
  - O nome aparece no ícone e na tela inicial do aparelho.
- **Testes:** varredura de cores e nome (novo, em `Tests.UI`), `StringsTests`.
- **Validação manual:** o usuário revisa as telas no aparelho com a paleta nova, com legibilidade sob sol e com a camisa dos times.
- **Não implementar:** logotipo final (D-29), arte 3D.

> A F7a não depende de F1–F6 e pode ser antecipada, a pedido do usuário. Só precisa ser coordenada com as etapas que mexem em `MatchHudView`, que são F1c e F6.

#### F7b — Arte 3D realista (C4, parte de arte)

> **X-123:** auditoria do estado gráfico e plano de máxima fidelidade ao estilo de transmissão realista (referência PES 2015, sem conteúdo da Konami) em `docs/VISUAL_DIRECTION.md`. Primeira etapa recomendada: cena representativa (V1). Proposta; não autoriza implementação nem antecipa a F7b.

- **Pré-requisitos:** **D-02**.
- **Escopo:**
  - Modelos de jogador com esqueleto humanoide único.
  - Animações de corrida, passe, chute, desarme e goleiro.
  - Estádio, iluminação, materiais e bola, tudo via Git LFS.
  - As animações só seguem o estado da simulação (CLAUDE §5.2).
  - Orçamento de conteúdo (TECHNICAL_SPEC §18).
- **Critérios de conclusão:**
  - As metas de FPS da §4 são atendidas no A07 e no S10+, com o perfil do Profiler anexado.
  - Nenhum pico de GC no loop.
  - A licença comercial de cada asset está registrada em `REFERENCES.md`.
  - O usuário aprova a aparência no aparelho.
- **Testes:**
  - PlayMode da cena Match (carrega e roda 1 minuto de partida sem erro).
  - Teste de que nenhum asset da partida usa Rigidbody ou Collider.
- **Validação manual:** sessão no aparelho com o Profiler.
- **Não implementar:** comemorações, replays, torcida dinâmica (pós-v1).

#### F7c — Níveis gráficos

- **Pré-requisitos:** F7b; **D-27**.
- **Escopo:**
  - Perfis URP por nível (sombras, resolução, pós-processamento, LOD).
  - Seleção em Configurações.
  - Escolha automática inicial por aparelho, se D-27 incluir.
- **Critérios de conclusão:**
  - Cada nível é medido nos 2 aparelhos: no nível mais baixo, o A07 fica com ≥ 30 fps estáveis; no padrão, o S10+ fica perto de 60.
  - A troca de nível vale sem reiniciar, ou no próximo carregamento, se D-27 decidir assim.
- **Testes:** persistência da configuração; PlayMode que troca o nível.
- **Validação manual:** tabela de FPS por nível e aparelho registrada.

#### F7d — Áudio (C4, parte de áudio)

- **Pré-requisitos:** F7b de preferência, mas não obrigatório.
- **Escopo:** torcida, apito e chute (MVP_SCOPE). A licença comercial de cada áudio fica registrada.
- **Critérios de conclusão:**
  - Os eventos-chave têm som.
  - Há volume em Configurações.
  - Nenhum áudio fica sem licença registrada.

### F8 — Polimento e teste fechado (ex-C5)

- **Pré-requisitos:** tudo acima; D-07, D-08, D-16 e D-29.
- **Escopo:** onboarding e tutorial, fichas da loja, política de privacidade, ícone e logotipo finais, performance final.
- **Analytics:** o C5 antigo previa analytics. Isso **conflita com "totalmente offline"** e fica PENDENTE até a D-07, que é a decisão de monetização e rede.
- **Critérios de conclusão:**
  - Crash-free > 99% no teste fechado.
  - Roda no aparelho de entrada.
  - Publicado em teste fechado.
  - O critério de sucesso da v1 (`PRODUCT_SPEC` §2.2) é verificado item por item com testadores.

---

## 4. Critérios de compatibilidade, desempenho e build Android

| Item | Critério | Fonte | Situação |
| --- | --- | --- | --- |
| Arquitetura | ARM64, IL2CPP | X-03 | Configurado |
| Android mínimo | `minSdk 25` hoje; versão final em D-08, conforme as regras vigentes da Play Store | D-08 | Pendente |
| Aparelhos de referência | Entrada: Galaxy A07. Intermediário: Galaxy S10+. Topo (não conta para o aceite): S25 Ultra | X-63 | Decidido |
| FPS da partida | **≥ 30 estáveis** no A07, **perto de 60** no S10+. Queda em lance pior que 30 constante reprova | TECHNICAL_SPEC §3 | Atendido em 14/10 só com primitivas; precisa ser medido de novo em F0 e em toda etapa de apresentação |
| FPS dos menus | Meta 60, limitado em tela parada | TECHNICAL_SPEC §3 | Não medido |
| Alocação | Zero por frame no loop da partida | CLAUDE §10 | Testado (`Step_AllocatesNoMemory`) |
| Orientação | Menus em retrato, partida em paisagem | TECHNICAL_SPEC §3 | Implementado |
| Telas | Layout em 16:9, 20:9 e 4:3, com área segura (notch) | brief §5 | Não verificado |
| Offline | Nenhum `UnityWebRequest`, `HttpClient` ou `System.Net` | brief §10 | Verificado por varredura; acrescentar teste de arquitetura (F0 ou F2b) |
| Dados | `Data/` empacotado por etapa de build (D-09), nunca `StreamingAssets` como fonte | D-09 | `DataPackBuildStep` existe; conferir em F0 item 8 |
| Save | Abrir saves de versões anteriores; escrita atômica; 3 backups | B8 | Testado |
| Build | Antes de concluir as etapas F0, F1b, F1c, F2b, F6, F7b, F7c e F8: APK de desenvolvimento instalado nos 2 aparelhos e FPS anotado em `DECISIONS.md` | Este plano | — |

**Nunca declarar** validação Android ou teste físico sem a execução registrada (data, aparelho, build e resultado).

---

## 5. Riscos técnicos e mitigação

| Risco | Nível | Mitigação |
| --- | --- | --- |
| Arte realista não cabe no aparelho de entrada (22 humanoides animados) | **Alto** | Decidir a D-02 cedo. Prova de conceito com 22 jogadores animados no A07 antes de comprar ou produzir o conteúdo inteiro. Plano B: animação assada em textura, LOD, sombras blob (TECHNICAL_SPEC §18). Níveis gráficos (F7c) |
| Origem e licença da arte e do áudio | Alto | Licença comercial registrada asset por asset em `REFERENCES.md`; nada sem licença no repositório |
| Mudar a duração para 3–5 min quebra a calibração e as metas (testes em 6 min) | Médio | F1a isolada, antes da F1b; faixas de teste em dados; calibração completa (Slow) antes de concluir |
| Ataque da IA desequilibra defesa, goleiro ou sensibilidade | Médio | `SensitivityTests` e calibração bloqueiam a conclusão; parâmetros em dados |
| Dificuldade muda a QuickSim sem querer e a carreira simulada fica injusta | Médio | O nível de referência reproduz a IA atual com a mesma semente (teste); a QuickSim só muda por decisão |
| Treino infla o OVR e quebra a progressão D→A | Médio | Potencial como teto; testes de 10 temporadas com faixas por série; parâmetros em `training.json` |
| Mercado do usuário quebra a economia | Médio | Invariantes (saldo = livro, 1 contrato) e testes de 10–30 temporadas |
| Várias migrações de save seguidas (F1c, F3, F4, F5) | Médio | Uma mudança de schema por etapa, em sequência; fixture de cada versão; teste de abrir o save mais antigo |
| Cobertura mínima de testes do lado Unity (HUD, câmera, input) | Médio | PlayMode novos em F2b, F6 e F7b; modelos de layout e HUD no código puro, testáveis por `dotnet test` |
| Testes lentos (calibração ~15 min, suíte completa ~16 min) | Baixo–médio | Categoria `Slow` antes de concluir uma etapa, não a cada commit |
| Teste de tempo `Simulation_RunsInMilliseconds` falha com a máquina ocupada | Baixo | Rodar isolado ao investigar; avaliar uma margem (exige decisão, não fazer em silêncio) |
| Ambiente .NET 10 × alvo .NET 8 | Baixo | D-33 |
| Divergência entre sessões e branches (X-94) | Médio | Uma linha principal definida em F0; push só com branch nova ou fast-forward; `git ls-remote` antes de integrar |
| Escopo de uma pessoa só | Alto | Etapas pequenas com critério verificável; nada fora do `PRODUCT_SPEC` sem decisão; D-31 e D-32 não ampliam o escopo enquanto estiverem pendentes |
| UI Toolkit em mobile (listas longas no mercado) | Médio | Lista virtualizada em F5; medir no A07 |

---

## 6. Ordem recomendada e progresso

**Ordem:**

```
F0 → F1a → F1b → F1c → F2a → F2b → F3 → F4 → F5 → F6 → F7a → F7b → F7c → F7d → F8
```

A F6 e a F7a podem andar em paralelo à F3–F5, se houver uma segunda sessão (não tocam no save nem na carreira). A F7a pode ser antecipada, a pedido.

**Decisões necessárias para começar** (só estas):

| Para começar | Decisão |
| --- | --- |
| F0 | Os itens ★ |
| F1a | D-22 |
| F1c | D-23 |
| F3 | D-24 |
| F4 | D-25 |
| F5 | D-30 |

As outras decisões podem esperar até a etapa delas.

**Progresso:**

- **O percentual atual (72%) não muda:** mede o roadmap antigo até a C5, com pesos fixos (`ROADMAP.md`).
- **Proposta para aprovação, não aplicada:**
  1. Manter a tabela antiga como "legado".
  2. Criar a medida "Football Nation v1" com pesos por etapa.
  3. Conceder crédito só a etapas concluídas e validadas.

  Critério da proposta:

  - As etapas antigas mantêm o peso que já tinham: Stage 0 = 2, A1–A9 = 42, B1–B8 = 24, C1 = 5 e C2 = 4, num total de 77.
  - A C3 (12) é dividida: C3a = 3 e C3b = 3. A C3c vira F5, a C3d vira F2a e a C3e é repartida entre F2b e F7a.
  - A C4 vira F7b e F7d; a C5 vira F8.
  - As etapas novas recebem pesos estimados, como os antigos (estimativa de esforço, não medição).

  | Etapa | Peso | Etapa | Peso | Etapa | Peso |
  | --- | --- | --- | --- | --- | --- |
  | Stage 0, A, B, C1, C2 (antigas) | 77 | F2a | 3 | F7a | 2 |
  | C3a | 3 | F2b | 3 | F7b | 8 |
  | C3b | 3 | F3 | 4 | F7c | 2 |
  | F0 | 2 | F4 | 4 | F7d | 2 |
  | F1a | 2 | F5 | 4 | F8 | 6 |
  | F1b | 3 | F6 | 4 | | |
  | F1c | 3 | | | **Total** | **135** |

  **Nesta proposta, o progresso de hoje seria 75/135 ≈ 56%:** Stage 0, A, B e C2 (72), mais a C3a (3).

  - A C1 (+5) e a C3b (+3) entram quando o usuário as validar (F0), o que daria ≈ 61%.
  - A proposta só passa a valer se o usuário aprovar.
  - **A diferença em relação aos 72% vem só do escopo maior:** treino, criar clube, controles, dificuldade e arte realista. Não é perda de trabalho.
