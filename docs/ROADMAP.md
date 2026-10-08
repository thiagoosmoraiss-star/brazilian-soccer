# ROADMAP.md

Fonte: Especificação Técnica §20. **O Claude Code trabalha somente na etapa atual.** Nada de etapas futuras, mesmo que pareça rápido ou útil. Cada etapa termina quando cumpre seus critérios e o `DEFINITION_OF_DONE.md`.

**Etapa atual:** A7 — A7a concluída; ajuste de ataque da IA feito (X-58, em validação); falta a A7b (ver `CLAUDE.md` §14). A1–A6 concluídas. Track B concluída (B1–B8). D-01 decidida: Track B primeiro. Com uma pessoa só, as trilhas são sequenciais; em paralelo apenas se houver mais de um desenvolvedor.

Regras comuns a todas as etapas:

- Testes da etapa executáveis por linha de comando; nenhum teste anterior quebrado.
- Balanceamento em JSON; nenhum número mágico.
- Zona pura sem Unity; zero alocação por frame no loop da partida (a partir de A1).
- Pendências que bloqueiam a etapa precisam estar resolvidas antes de começar.
- `Data/` na raiz é a fonte da verdade (D-09). A primeira etapa cuja build Android precisar ler `Data/` inclui a etapa de build/empacotamento que copia os dados para dentro do app, sem duplicar a fonte nem usar `StreamingAssets` como fonte.
- Binários/pesados entram via Git LFS (D-06).

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
- **Sistemas:** Referee (faltas por contato de desarme e disputa, cartões, impedimento no momento do passe), Restart completo (falta indireta, falta direta sem curva, escanteio com regiões, pênalti batendo e defendendo).
- **Módulos:** `Match`, `Rules`.
- **Testes:** impedimento correto em cenários; falta por contato antes da bola; 2 amarelos = vermelho; pênalti executa e termina; bola parada nunca trava.
- **Aceite:** partidas headless com taxas de faltas/cartões dentro de faixas definidas em dados; testes verdes.
- **Não implementar:** carrinho (A9), curva em falta, barreira editável, VAR, vantagem.

### A9 — Resto da partida do MVP
- **Objetivo:** fechar a partida do MVP (`MVP_SCOPE.md`).
- **Pré-requisitos:** A8; D-17 (dificuldade da partida no MVP).
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
