# ROADMAP.md

Fonte: Especificação Técnica §20. **O Claude Code trabalha somente na etapa atual.** Nada de etapas futuras, mesmo que pareça rápido ou útil. Cada etapa termina quando cumpre seus critérios e o `DEFINITION_OF_DONE.md`.

**Etapa atual (19/10/2026):** Etapa 2 do Football Nation, só documentação (X-110). A especificação oficial está em `PRODUCT_SPEC.md` e o plano das etapas F0–F8 em `IMPLEMENTATION_PLAN.md`, que **aguarda aprovação do usuário**. Nenhuma etapa F foi iniciada.

- Da trilha anterior: A1–A9 e B1–B8 concluídas; C2 concluída; C1 implementada, aguardando a validação da lesão em campo; C3a validada; C3b implementada e não validada.
- As etapas C3c–C5 abaixo foram **reorganizadas** nas etapas F (ver "Track F" no fim deste arquivo). O texto antigo fica como histórico.
- Com uma pessoa só, as trilhas são sequenciais; em paralelo apenas se houver mais de um desenvolvedor.

Regras comuns a todas as etapas:

- Testes da etapa executáveis por linha de comando; nenhum teste anterior quebrado.
- Balanceamento em JSON; nenhum número mágico.
- Zona pura sem Unity; zero alocação por frame no loop da partida (a partir de A1).
- Pendências que bloqueiam a etapa precisam estar resolvidas antes de começar.
- `Data/` na raiz é a fonte da verdade (D-09). A primeira etapa cuja build Android precisar ler `Data/` inclui a etapa de build/empacotamento que copia os dados para dentro do app, sem duplicar a fonte nem usar `StreamingAssets` como fonte.
- Binários/pesados entram via Git LFS (D-06).


## Progresso

Pedido do usuário (13/10/2026): a cada etapa concluída, informar a porcentagem do roadmap. Pesos fixos por etapa, proporcionais ao esforço **estimado** (estimativa, não medição; só mudam com registro em `DECISIONS.md`). Uma etapa conta só quando concluída e validada; etapa em andamento conta 0. O total mede o caminho até o teste fechado na loja (C5); produção de arte além do placeholder depende de D-02 e não está nos pesos.

| Etapa | Peso | Etapa | Peso | Etapa | Peso |
| --- | --- | --- | --- | --- | --- |
| Stage 0 | 2 | B1 | 3 | C1 | 5 |
| A1 | 3 | B2 | 3 | C2 | 4 |
| A2 | 4 | B3 | 3 | C3 | 12 |
| A3 | 4 | B4 | 3 | C4 | 5 |
| A4 | 5 | B5 | 3 | C5 | 6 |
| A5 | 4 | B6 | 3 | | |
| A6 | 3 | B7 | 3 | | |
| A7 | 6 | B8 | 3 | | |
| A8 | 5 | | | | |
| A9 | 8 | | | **Total** | **100** |

**Concluído:** Stage 0, A1–A9, B1–B8, C2 = **72%** (16/10/2026). C1 implementada e validada no editor; conclui com a lesão em campo (X-91, sessão auxiliar, X-94).

**19/10/2026:** o percentual **não mudou**. A lesão em campo foi juntada (X-98), mas ainda não foi validada pelo usuário. Esta tabela mede o caminho antigo até a C5. A medida nova para o Football Nation v1 está **proposta** em `IMPLEMENTATION_PLAN.md` §6, com os critérios dela: ≈ 56% hoje, pelo escopo maior. Ela só substitui esta se o usuário aprovar.

## Situação por etapa e funcionalidade (19/10/2026)

Legenda:

- **Implementado e testado:** teste automatizado executado e validação do usuário registrada em `DECISIONS.md`, quando a etapa exige.
- **Implementado, não validado manualmente:** testes automatizados verdes, sem validação no editor ou aparelho.
- **Parcial:** parte implementada, parte faltando.
- **Planejado:** etapa definida no `IMPLEMENTATION_PLAN.md`, sem decisão pendente que a bloqueie, mas não iniciada.
- **Bloqueado:** depende de uma decisão pendente do usuário.

| Situação | Itens |
| --- | --- |
| **Implementado e testado** | Stage 0; A1–A9 (partida completa do MVP; aparelho e editor: X-63, X-74, X-76, X-82); B1–B8 (carreira, QuickSim, mercado, economia, diretoria e save nas regras); C2 (calibração); C1 sem a lesão (X-90); C3a (X-96) |
| **Implementado, não validado manualmente** | Lesão em campo e troca forçada (X-100, juntada em X-98); C3b: elenco, perfil, escalação e tática do usuário (X-97). Também: nenhuma build Android do commit atual (a última é de 14/10) e testes rodados no .NET 10 com roll-forward (D-33) |
| **Parcial** | C1 (falta validar a lesão); C3 (C3a ok, C3b sem validação, C3c–C3e não feitas); placar da partida (~0,65 gol em 4 min e ~1,1 em 6 depois da F1b, X-115; metas experimentais ainda acima); duração (3/4/5/6/10 com padrão 4 nos dados, F1a em andamento); mercado, economia, obras e staff (regras prontas, sem comandos do usuário); câmera e HUD (sem teste automatizado, textos e cores no código); competições (pontos corridos configuráveis; nomes antigos); save (falta o aviso de backup restaurado na UI) |
| **Em andamento** | F0: executada em 19/10/2026 até onde não depende do usuário (`F0_2026-10-19.md`). Faltam a validação no editor, a instalação nos aparelhos e as autorizações dos itens ★ |
| **Planejado** | F2a e F2b (carreira funcional pela UI); F6 (personalização de controles; o mapeamento depende de D-26); F7a (nome e tema; as cores de estado dependem de D-28); F7d (áudio); F8 (D-07, D-08, D-16 e D-29 antes da loja) |
| **Em andamento (F1)** | F1 autorizada em X-112. **F1a:** duração 3/4/5/6/10 com padrão 4, metas experimentais em dados e testes feitos (X-113). Com a D-34 (X-114), o lote de calibração fica em 6 min, com as tolerâncias intactas, até o fim da F1b; as 9 falhas em 4 min são diagnóstico conhecido. Falta a validação no editor. A F1b só conclui com a calibração aprovada em 4 min. **F1b** (X-115): ataque da IA implementado (valor de posse, atacar a área, corredor e alcance de chute, faltas na nova duração, correção da perseguição fora do campo); em 4 min 0,68 gol (era 0,42) e 3,56 finalizações (era 2,51). Continuação (X-116, D-35 (c)): lote de calibração de 1 200 partidas, bloqueio de chute; em 4 min 0,68–0,75 gol e 3,5–3,7 finalizações (antes 0,45–0,48 e 2,4–2,5). Testes oficiais verdes com o lote em 6 e em 4 min; por semente, 4 de 6 passam em cada duração (posse por força frágil, D-36). S-07 avaliada, não resolvida (D-37); escala de faltas a consolidar (D-38). Decisões do usuário (X-117): **D-36 (a)** erro de domínio (Drible + Compostura), **D-37 (b)** saída longa sob pressão e atacantes de área por mentalidade, **D-38** faltas 3,0 consolidada; corrigido um impasse de posse (bola parada que ninguém podia tomar). Calibração por semente **6 de 6 em 4 e em 6 min**; S-07 aceita como limitação conhecida (X-118: a ofensiva continua a mais eficiente em pontos). Validação final (X-118): automatizados verdes, compilação e PlayMode na Unity ok, APK gerado. **F1b concluída (X-119)**: validação manual no aparelho e no editor relatada pelo usuário sem defeito impeditivo. **F1c** (X-120): dificuldade Fácil/Normal/Difícil implementada (só o adversário do usuário, Normal idêntico à IA atual, QuickSim só no confronto do usuário, escolha na nova carreira e em Configurações, save v4); automatizados verdes (746/746), compilação na Unity ok, APK gerado. **F1c concluída (X-121)**: validação manual no aparelho aprovada pelo usuário (diferenças perceptíveis entre os níveis; Difícil com desafio elevado). F2 não começou |
| **Bloqueado por decisão pendente** | F3, F4 e F5: as decisões de base foram tomadas em X-111 (D-24, D-25, D-30), mas faltam os detalhes de cada etapa; F7b arte realista (**D-02**); F7c níveis gráficos (**D-27**, depois da D-02); formatos de campeonato além de pontos corridos (**D-31**); permanência de Copa, diretoria e staff (**D-32**, mantidos até lá) |

---

## Stage 0 — Foundation

- **Objetivo:** base comum para as duas trilhas: projeto, assemblies, dados, RNG, loop de passo fixo e testes.
- **Pré-requisitos:** D-06 (Git + Git LFS), D-09 (`Data/` na raiz) e D-19 (contratos no `Core`) — decididas; D-11 (serializador) continua pendente e só bloqueia se o loader de JSON precisar dele já nesta etapa.
- **Sistemas:** Core (Ids, RNG com fluxos, log, math via System.Numerics, resultado de operação), Data (loader e validador de JSON, GameDatabase vazio), Balance (Effect, EffectDefinition, curvas por partes, Eval, modificadores), loop de passo fixo genérico, debug draw.
- **Módulos esperados:** repositório Git com Git LFS configurado; projeto Unity 6 LTS com asmdefs de todas as assemblies de `REPOSITORY_STRUCTURE.md` (vazias onde ainda não há conteúdo); solution .NET paralela compilando as puras; `Data/Balance/` na raiz do repositório com schema e exemplos mínimos; `Tests/Unit`. Os contratos `MatchSetup`/`MatchResult` ficam no `Core`, criados na primeira etapa que precisar deles (não nesta).
- **Testes:** RNG (reprodutibilidade, independência de fluxos); curvas (interpolação, limites, monotonicidade quando declarada); validador (rejeita JSON inválido, Effect sem definição, atributo sem efeito); loop fixo (nº de passos independente do FPS); teste que falha se assembly pura referenciar UnityEngine (garantido pela solution .NET).
- **Aceite:** `dotnet test` verde; o loader lê `Data/` da raiz do repositório tanto no editor Unity quanto na solution .NET; projeto Unity abre e compila; build Android vazio instala no aparelho de referência (se D-03 já decidida; senão, qualquer aparelho).
- **Não implementar:** nenhuma regra de futebol, entidade de carreira, UI, cena de jogo, save.

---

## Track A — Match

### A1 — Física da bola
- **Objetivo:** bola determinística e previsível.
- **Pré-requisitos:** Stage 0.
- **Sistemas:** Pitch (geometria 105×68), Ball (rolando, aérea, quique, trave/travessão por segmento, rede, gol, saída, efeito como parâmetro), previsão de trajetória, BallConfig em JSON.
- **Módulos:** `Match` (Pitch, Ball), `Data/Balance/ball.json`, cena sandbox de física (dev).
- **Testes:** previsão = integração dentro de tolerância; energia não aumenta sem impulso; nunca atravessa trave; gol só com cruzamento completo da linha; saída detectada; determinismo por semente.
- **Aceite:** sandbox chuta com parâmetros e mostra trajetória prevista vs real; todos os testes verdes.
- **Não implementar:** jogadores, posse, passes, IA, câmera broadcast.

### A2 — Movimento e posse
- **Objetivo:** um jogador controlado por toque conduzindo a bola.
- **Pré-requisitos:** A1; D-10 (Cinemachine ou câmera própria).
- **Sistemas:** PlayerBody, Movement (aceleração, curvas, sprint, com bola), Fatigue (normalizada pela duração), Possession (toques, raio de controle, bola solta), DribbleSystem (condução, corte, arrancada, proteção, parar), InputAdapter (analógico flutuante, sprint com memória, buffer), câmera de jogo mínima.
- **Módulos:** `Match`, `Input`, `Presentation` (view mínima), `movement.json`, `fatigue.json`.
- **Testes:** tempos de aceleração e perdas em curva dentro das curvas; fadiga total igual em 4/6/10 min; posse só muda por evento válido; zero alocação no passo.
- **Aceite:** conduzir, cortar e arrancar no aparelho com resposta imediata; testes verdes.
- **Não implementar:** passe, chute, finta de corpo, outros jogadores com IA, adversários.

### A3 — Passe e chute
- **Objetivo:** 2–3 jogadores trocando passes e chutando a gol vazio.
- **Pré-requisitos:** A2.
- **Sistemas:** PassSystem (curto, longo rasteiro, enfiada; cone Semi; erro por fatores), ShotSystem (força, direção, pressão, pé ruim, equilíbrio), ControlSelection (controle vai ao recebedor), Rules (efeitos correspondentes via Balance).
- **Módulos:** `Match`, `Rules`, `effects.json`.
- **Testes de cenário:** passe de 20 m sem pressão chega; erro cresce com pressão e pé ruim; chute do meio-campo raramente é preciso; atributos alteram erro na direção esperada.
- **Aceite:** passar e finalizar no aparelho; testes verdes.
- **Não implementar:** Alto (lançamento/cruzamento), colocado, cabeceio, goleiro, IA de time.

### A4 — Posicionamento da IA
- **Objetivo:** 11×11 se movendo por formação e fase de jogo.
- **Pré-requisitos:** A3.
- **Sistemas:** Match.AI camada do time (fases), camada da função (posição-alvo, compactação, impedimento como limite), camada individual básica (apoio, marcação por zona), decisão com bola da IA (conduzir/passar/chutar), uma formação.
- **Módulos:** `Match.AI`, `formations/*.json` (uma formação), `tactics.json` (valores padrão), modo headless.
- **Testes:** sem oscilação de intenção; sem "bolo"; compactação respeitada; partida headless termina; Posicionamento baixo gera mais erro de alvo.
- **Aceite:** 11×11 headless e renderizado com movimento coerente; testes verdes.
- **Não implementar:** ruptura, cobertura refinada, marcação na área, mudanças táticas, goleiro.

### A5 — Defesa e troca
- **Objetivo:** defender com o jogador controlado.
- **Pré-requisitos:** A4.
- **Sistemas:** DefenseSystem (contenção, desarme em pé automático, interceptação), Contact (separação), ControlSelection completa (troca automática com histerese e travas, troca manual por toque, anel do próximo).
- **Módulos:** `Match`, `Match.AI`, `Input`.
- **Testes:** troca nunca durante contenção; histerese evita alternância; desarme só com bola alcançável.
- **Aceite:** defender e recuperar a bola no aparelho; nenhuma troca claramente errada em sessões gravadas.
- **Não implementar:** carrinho, 2º defensor, faltas, cartões, troca direcional, níveis de assistência além de Semi.

### A6 — Goleiro
- **Objetivo:** goleiros do vertical slice.
- **Pré-requisitos:** A5.
- **Sistemas:** Goalkeeper (posicionamento, reação, alcance, encaixe/espalma, rebote).
- **Módulos:** `Match.AI`.
- **Testes:** defende chute fraco central; não alcança fora do alcance; tempo de reação por Reflexo; nunca anda para dentro do gol.
- **Aceite:** testes verdes; goleiro plausível em partidas gravadas.
- **Não implementar:** saída 1×1, cruzamentos, erros especiais, reposição tática.

### A7 — Reinícios simples + placar → Vertical Slice
- **Objetivo:** partida completa do vertical slice (`TECHNICAL_SPEC.md` §19).
- **Pré-requisitos:** A6; D-03 (aparelhos de referência) para medir aceite; D-05 decidida (X-55: tiro de meta só curto); D-02 não é exigida (placeholders são aceitos no VS).
- **Dividida em duas entregas (X-56):** **A7a** — reinícios simples, MatchClock 2×2 min, placar, estatísticas/`MatchResult`, snapshot do `MatchState` (restauração idêntica), relatório headless de 200 partidas; **A7b** — cena Match, HUD mínimo, pausa + retomar após segundo plano no aparelho, câmera broadcast + radar, tela final, animações básicas e o aceite de desempenho (D-03).
- **Sistemas:** Restart simples (saída, lateral, tiro de meta, escanteio curto), MatchClock (2×2 min), placar, tela final, pausa + retomar após segundo plano (snapshot de `MatchState`), câmera broadcast + radar, animações básicas, relatório headless de 200 partidas.
- **Módulos:** `Match`, `Presentation`, `UI` (HUD mínimo), `App` (cena Match), `Tools` (relatório headless).
- **Testes:** ver aceite do VS.
- **Aceite (VS):** ≥ 30 fps no aparelho de entrada e perto de 60 no intermediário; testadores novos marcam gol na 1ª partida; forte vence a maioria em 200 headless com algumas zebras; alterar Velocidade/Passe/Finalização move o relatório na direção esperada; nenhuma troca automática claramente errada em 10 partidas; todos os testes por CLI.
- **Não implementar:** faltas, cartões, impedimento, pênalti, substituições, táticas, QuickSim, carreira.

### A8 — Árbitro e bola parada completa
- **Objetivo:** regras completas da partida do MVP.
- **Pré-requisitos:** A7.
- **Dividida em duas entregas (X-64):** **A8a** — regras e IA: faltas, cartões e expulsão, impedimento, falta indireta, falta direta com barreira, pênalti batido e defendido pela IA, taxas em partidas headless (X-65: ~10 faltas e ~2 amarelos por partida, também na QuickSim); **A8b** — controles do usuário nas bolas paradas (mira/força na falta direta e no pênalti, escolher o lado ao defender pênalti) e aviso de cartão/expulsão na tela. O escanteio com regiões depende do Alto (cruzamento) e entra com ele na A9 (X-66).
- **Sistemas:** Referee (faltas por contato de desarme e disputa, cartões, impedimento no momento do passe), Restart completo (falta indireta, falta direta sem curva, escanteio com regiões, pênalti batendo e defendendo).
- **Módulos:** `Match`, `Rules`.
- **Testes:** impedimento correto em cenários; falta por contato antes da bola; 2 amarelos = vermelho; pênalti executa e termina; bola parada nunca trava.
- **Aceite:** partidas headless com taxas de faltas/cartões dentro de faixas definidas em dados; testes verdes.
- **Não implementar:** carrinho (A9), curva em falta, barreira editável, VAR, vantagem.

### A9 — Resto da partida do MVP
- **Objetivo:** fechar a partida do MVP (`MVP_SCOPE.md`).
- **Pré-requisitos:** A8; D-17 (dificuldade da partida no MVP) — decidida em X-72: nível único.
- **Dividida em três entregas (X-72):** **A9a** (validada, X-74) bola alta (Alto, cabeceio, escanteio com cruzamento, goleiro em cruzamentos, tiro de meta e lateral longos); **A9b** (validada, X-76) carrinho, 2º defensor, disputa de corpo, ruptura e cobertura, saída do goleiro no 1×1; **A9c** (validada, X-82) táticas (3 formações, mentalidade, linha, pressão), substituições e sensibilidade de todos os atributos.
- **Sistemas:** Alto (lançamento, cruzamento alto), cabeceio, carrinho, 2º defensor, disputa de corpo básica, ruptura e cobertura, substituições (regras + IA), TacticsRuntime (3 formações, mentalidade, linha, pressão).
- **Módulos:** `Match`, `Match.AI`, `formations/*.json`, `tactics.json`.
- **Testes:** mudança tática altera posições médias; substituições respeitam limite; cabeceio disputado pela regra; sensibilidade de cada atributo da partida.
- **Aceite:** partida do MVP jogável completa; testes verdes.
- **Não implementar:** colocado, cruzamento rasteiro, enfiada aérea, finta de corpo, troca direcional, assistências Completa/Manual, presets, largura/laterais, Assistir — tudo pós-MVP.

---

## Track B — Career

### B1 — Mundo e dados
- **Objetivo:** gerar um mundo fictício válido.
- **Pré-requisitos:** Stage 0; D-12 (tamanho do mundo no MVP); D-15 (traits) ou decisão explícita de gerar sem traits por ora.
- **Sistemas:** gerador de mundo (clubes com UF, cores, template de escudo, reputação, orçamento, estádio, torcida; jogadores com 18 atributos, potencial, posições, idade), modelos da §4 da especificação, cálculo de OVR.
- **Módulos:** `Career` (entidades, gerador), `Rules` (OVR), `Data/World/*.json` (pools, templates).
- **Testes:** mundo válido para várias sementes; distribuição de OVR por divisão nas faixas do GDD; Ids únicos; mesma semente = mesmo mundo.
- **Aceite:** gerar e inspecionar mundo por ferramenta de linha de comando; testes verdes.
- **Não implementar:** partidas, calendário, mercado, economia, save.

### B2 — QuickSim
- **Objetivo:** resolver partidas em milissegundos com `MatchSetup` → `MatchResult`.
- **Pré-requisitos:** B1.
- **Sistemas:** força de time por setor (Rules), QuickSim por posses/fatias, eventos (gols, finalizações, faltas, cartões, escanteios, lesões), fadiga, IA de substituição, notas.
- **Módulos:** `Simulation`, `Rules`, `quicksim.json`.
- **Testes:** lotes de 1.000–10.000 partidas: gols e % de 0×0 nas faixas de design, curva diferença de OVR → resultado, sensibilidade por atributo, resultados impossíveis ausentes.
- **Aceite:** metas de design atingidas; testes verdes.
- **Não implementar:** calibração contra MatchEngine (C2), calendário.

### B3 — Calendário e competições
- **Objetivo:** temporada completa com acesso e rebaixamento.
- **Pré-requisitos:** B2; D-13 (sobe/desce com 16 clubes) e D-14 (classificação para a Copa).
- **Sistemas:** CompetitionFormat (pontos corridos, mata-mata jogo único), CompetitionEdition, calendário por datas, Matchday, SeasonTransition, desempates.
- **Módulos:** `Career`, `Data/Competitions/*.json`.
- **Testes:** 10 temporadas: nº de clubes por divisão constante; sobe/desce bate com a tabela; calendário válido (todos jogam tudo, sem 2 jogos na mesma data, intervalo mínimo).
- **Aceite:** 10 temporadas em `dotnet test` sem Unity; testes verdes.
- **Não implementar:** estaduais, grupos, quadrangulares, continental, mercado, economia.

### B4 — Jogadores na carreira
- **Objetivo:** jogadores evoluem e se desgastam.
- **Pré-requisitos:** B3.
- **Sistemas:** Development (curva de idade, minutos, CT), Condition (energia, moral simples, forma), Injury (simples), suspensões, aposentadorias, Youth (3 jovens/ano fixos).
- **Módulos:** `Career`, `development.json`.
- **Testes:** jovens de potencial alto atingem faixa até 24 anos; ninguém passa do potencial; veteranos caem; lesões e suspensões aplicadas corretamente.
- **Aceite:** 10 temporadas sem OVR explodindo ou colapsando; testes verdes.
- **Não implementar:** traits na carreira além do que D-15 decidir; níveis de base; depto. médico.

### B5 — Mercado
- **Objetivo:** mercado do MVP.
- **Pré-requisitos:** B4.
- **Sistemas:** valor de mercado, salário de referência, interesse do jogador, negociação (ClubStage → PlayerStage), contratos (vencimento, pré-contrato, renovação), agentes livres, propostas da IA ao jogador, scouting simples por faixa, IA de mercado com necessidades, índice e limites, reposição por jogadores gerados.
- **Módulos:** `Career.Market`, `market.json`.
- **Testes:** elencos entre 20–26; inflação de valor/salário dentro do limite; negociação completa válida; jogador nunca com dois contratos ativos.
- **Aceite:** 10 temporadas com mercado estável; testes verdes.
- **Não implementar:** empréstimos, cláusulas, parcelas, trocas, rede de olheiros.

### B6 — Economia
- **Objetivo:** dinheiro com significado.
- **Pré-requisitos:** B5.
- **Sistemas:** livro-caixa, TV, bilheteria, 1 patrocinador, premiações, salários, manutenção, orçamento (teto e verba), caixa negativo.
- **Módulos:** `Career.Economy`, `economy.json`.
- **Testes:** saldo = soma do livro; 10–30 temporadas sem caixa infinito ou falência sistemática; clube do jogador pode quebrar gastando mal.
- **Aceite:** testes verdes.
- **Não implementar:** loja/produtos, preço de ingresso ajustável, remanejamento de verba, empréstimo bancário.

### B7 — Diretoria, reputação, instalações, staff
- **Objetivo:** progressão do MVP.
- **Pré-requisitos:** B6; D-17 (dificuldade no MVP).
- **Sistemas:** objetivos da temporada, confiança, demissão, reputação de clube e manager, Estádio e CT com obras e requisito por divisão, staff (preparador, auxiliar, olheiro), notícias.
- **Módulos:** `Career`, `Data/Board/*.json`, `facilities.json`, `staff.json`.
- **Testes:** D→A leva 6–10 temporadas em simulação de política automática; demissão ocorre quando esperado; requisito de estádio aplicado.
- **Aceite:** testes verdes.
- **Não implementar:** objetivos de longo prazo, sala de troféus, depto. médico, base com níveis, loja.

### B8 — Save
- **Objetivo:** persistência robusta.
- **Pré-requisitos:** B3 (começa cedo e evolui junto das etapas seguintes); D-11 (serializador).
- **Sistemas:** serialização JSON + gzip, cabeçalho, `schemaVersion`, migrações, validação (checksum, integridade, invariantes), escrita atômica, 3 backups, autosave (política), `IFileStore`.
- **Módulos:** `Save`, `Tests/Save/Fixtures`.
- **Testes:** ida e volta idêntica; migração de fixtures; corrupção simulada recupera backup; invariantes.
- **Aceite:** testes verdes; cada etapa B posterior mantém save funcionando.
- **Não implementar:** nuvem, múltiplos saves além do MVP (1 save local).

---

## Convergence

### C1 — Integração
- **Objetivo:** pré-jogo → partida jogável → resultado aplicado na carreira.
- **Pré-requisitos:** A7 + B3.
- **Sistemas:** montagem de `MatchSetup` pela carreira; `MatchResult` do MatchEngine aplicado; cenas Boot/Shell/Match; snapshot de partida no save.
- **Testes:** resultado jogado aplica tabela, cartões, lesões e energia igual a um resultado simulado; retomar partida após fechar o app.
- **Aceite:** temporada com partidas jogadas e simuladas misturadas.
- **Não implementar:** telas finais de UI (C3), áudio (C4).

### C2 — Calibração QuickSim × MatchEngine
- **Objetivo:** os dois motores produzem distribuições compatíveis.
- **Pré-requisitos:** A9 + B2.
- **Testes:** gols, 0×0, finalizações, escanteios, faltas, cartões, posse, curva OVR → resultado, sensibilidade por atributo dentro das tolerâncias em dados.
- **Aceite:** teste de calibração verde e bloqueando merge.
- **Não implementar:** mudanças de design para "fechar" a calibração sem registro em `DECISIONS.md`.

### C3 — UI
- **Objetivo:** todas as telas do MVP na Shell.
- **Pré-requisitos:** B7; D-04 (idiomas); D-18 (criar clube).
- **Sistemas:** telas de `TECHNICAL_SPEC.md` §15, presenters, strings em tabelas, identidade visual básica.
- **Testes:** PlayMode de navegação; alvos de toque ≥ 44 pt; fim de partida → próxima em ≤ 3 toques.
- **Aceite:** carreira completa jogável pela UI.
- **Não implementar:** animações e cenas especiais (C4), telas de funcionalidades fora do MVP.
- **Divisão (X-95):** **C3a** base e ciclo do jogo (strings, `UI.Presenters`, Shell em pilha + barra inferior, inicial, escolha de clube, Hub, pré/pós-jogo, competições, calendário, configurações); **C3b** elenco (lista, perfil, escalação e tática do usuário na carreira); **C3c** mercado (comandos do usuário + telas Mercado/Negociação); **C3d** clube, diretoria e finanças (obras e staff pelo usuário); **C3e** identidade e aceite (fontes, PlayMode, alvos de toque, ≤ 3 toques, carreira completa pela UI). A C3 conta no progresso só com as cinco concluídas.

### C4 — Áudio e apresentação
- **Objetivo:** emoção básica.
- **Pré-requisitos:** C1; D-02 (arte/animações).
- **Sistemas:** torcida, apito, chute; cenas de acesso/título; manchetes.
- **Aceite:** momentos-chave com som e cena; nenhum áudio sem licença comercial.
- **Não implementar:** torcida dinâmica avançada, replays, comemorações (pós-MVP).

### C5 — Polimento
- **Objetivo:** pronto para teste fechado na loja.
- **Pré-requisitos:** tudo; D-07 (monetização), D-08 (Android mínimo), D-16 (nome).
- **Sistemas:** performance nos aparelhos de referência, onboarding, tutorial, analytics, fichas da loja, política de privacidade.
- **Aceite:** crash-free > 99% em teste fechado; roda no aparelho de entrada; publicado em teste fechado.
- **Não implementar:** funcionalidades novas.

---

## Track F — Football Nation v1 (proposta, X-110; aguarda aprovação)

Detalhes de cada etapa estão em `IMPLEMENTATION_PLAN.md` §3: escopo, critérios de conclusão, testes, validação manual e "não implementar". Os requisitos FN-xx estão em `PRODUCT_SPEC.md`.

**Ordem:**

```
F0 → F1a → F1b → F1c → F2a → F2b → F3 → F4 → F5 → F6 → F7a → F7b → F7c → F7d → F8
```

| Etapa | Conteúdo | Substitui | Bloqueio |
| --- | --- | --- | --- |
| F0 | Base do repositório: WIP, linha principal, worktrees, .NET, credencial; validação da lesão em campo e da C3b; build Android de linha de base | — | Autorização dos itens ★ |
| F1a | Duração 3/4/5 e recalibração | parte da C2 | D-22 |
| F1b | Ataque da IA, placar crível | X-84 (a) | F1a |
| F1c | Dificuldade ajustável que muda a IA | X-72 (1), X-45 (1) | D-23 |
| F2a | Clube, diretoria e finanças com comandos do usuário | C3d | D-32 resolvida (X-122 a X-124). **Implementada (X-127)**; falta a validação manual no editor |
| F2b | Ciclo de temporada pela UI, aviso de backup, textos da partida em tabela, ≤ 3 toques, alvos de toque | C3e (parte) | F2a |
| F3 | Criar clube e clubes editáveis | X-93, S-10 | D-24 |
| F4 | Treinamento | — (novo) | D-25 |
| F5 | Mercado do usuário, só Brasil | C3c | D-30 |
| F6 | Personalização de controles | — (novo) | D-26 (só o mapeamento) |
| F7a | Nome FOOTBALL NATION, tema com a paleta do brief, "Série A–D" | C3e (identidade) | D-28 (cores de estado) |
| F7b | Arte 3D realista | C4 (arte) | D-02 |
| F7c | Níveis gráficos | — (novo) | D-27 |
| F7d | Áudio | C4 (áudio) | — |
| F8 | Polimento e teste fechado | C5 | D-07, D-08, D-16, D-29 |

Enquanto o plano não for aprovado, **nenhuma etapa F pode ser iniciada**. A etapa atual continua sendo a documentação (X-110).
