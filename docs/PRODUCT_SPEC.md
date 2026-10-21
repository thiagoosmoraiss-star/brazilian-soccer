# PRODUCT_SPEC.md — Especificação oficial do Football Nation

Versão 1 — 19/10/2026 (Etapa 2: consolidação da documentação). Decisão de origem: **X-110** em `DECISIONS.md`.

Este documento é a **fonte de verdade do produto**: o que o Football Nation é e o que entra na primeira versão (v1). Ele não substitui a especificação técnica: arquitetura, tecnologia, estrutura de código, regras de dados, RNG, testes e performance continuam em `TECHNICAL_SPEC.md`, `ARCHITECTURE.md` e `CLAUDE.md`.

Documentos de entrada:

- `PRODUCT_BRIEF.md`: cópia fiel do brief do usuário (`Football Nation/docs/PRODUCT_BRIEF.md`). Mesmo SHA-256 em 19/10/2026.
- Lista de requisitos da Etapa 2 (mensagem do usuário de 19/10/2026), resumida em §2.
- `Football Nation/docs/LEGACY_AUDIT.md`: auditoria das versões antigas, com as contradições C-01 a C-14.
- O pacote antigo do projeto ACESSO: `GAME_DESIGN.md`, `MVP_SCOPE.md` e `DECISIONS.md`.

Regras de leitura:

1. **Quando este documento contradiz** `GAME_DESIGN.md` ou `MVP_SCOPE.md`, **vale este documento.** A regra antiga fica registrada como substituída (§5).
2. **Onde este documento não fala**, o `GAME_DESIGN.md` (design-alvo) e o `MVP_SCOPE.md` (itens do MVP antigo não contraditos) continuam válidos.
3. **Item marcado PENDENTE** não foi decidido. Ninguém escolhe por ele: a decisão está listada em §6 e em `DECISIONS.md`.

---

## 1. Identidade do produto

| Item | Especificação | Estado |
| --- | --- | --- |
| Nome | **FOOTBALL NATION**, com a mesma grafia em telas, documentação e materiais | Decidido (X-110). Verificação de marca e lojas: PENDENTE (D-16, parte restante) |
| Identificadores técnicos | Continuam como estão: `Game.*`, `brazilian-soccer`, `productName`, nomes de assemblies e o material `AcessoRuntimeMaterial`. Mudam só numa migração planejada (brief §3) | Decidido |
| Categoria | Futebol 3D com carreira de treinador e gestão de clube | Decidido |
| Plataforma | Android (ARM64, IL2CPP). iOS fica depois | Decidido (X-04) |
| Idioma | pt-BR no lançamento, com textos em tabela | Decidido (X-92) |
| Funcionamento | Totalmente offline e single-player, sem rede | Decidido (X-01) |
| Conteúdo | Clubes, jogadores, escudos e competições fictícios ou editáveis pelo usuário, sem marcas reais | Decidido (X-02, X-110) |
| Referência de experiência | Dream League Soccer, em acessibilidade, partida em tempo real, controles e evolução do elenco. Nada de recurso protegido, identidade, código ou conteúdo dele | Decidido (brief §1) |

### 1.1 Identidade visual

- **Paleta base:**

  | Cor | Hex | Uso |
  | --- | --- | --- |
  | Verde profundo | `#146B43` | base |
  | Amarelo dourado | `#F5C542` | destaque |
  | Azul | `#174A8B` | complementar |
  | Branco | `#F7F8FA` | neutro claro |

  Verde e neutros são a base. Evitar saturação excessiva e o uso das quatro cores ao mesmo tempo.
- **Cores fora da paleta:** cores de estado (positivo, negativo, alerta), os tons neutros escuros e a decisão de ter ou não um modo escuro: **PENDENTE (D-28)**.
- **Direção de arte dos menus:** acabamento moderno e esportivo, com personalidade brasileira (brief §3).
- **Logotipo, escudo do produto e ícone Android:** precisam ser legíveis em tamanho pequeno. Quem produz: **PENDENTE (D-29)**.
- **Identidade configurável:** paleta e nome vêm de dados, num tema único lido pelo loader. Nada de cor solta no código de UI (LEGACY_AUDIT §7 item 5).

---

## 2. Requisitos da v1

Legenda do estado no código (branch `integracao/c1p-lesao` @ `6c227af`):

- **✅ testado:** implementado, com teste automatizado e validado pelo usuário no editor ou aparelho.
- **🟦 não validado:** implementado e testado automaticamente, mas sem validação manual.
- **🟡 parcial.**
- **⬜ ausente.**

| Id | Requisito | Origem | Estado | Pendências |
| --- | --- | --- | --- | --- |
| FN-01 | Nome, paleta e identidade visual do Football Nation | brief §3, Etapa 2 | ⬜ O código ainda mostra "ACESSO" e a paleta antiga (`UiKit.cs` e 75 cores fixas em 16 arquivos) | D-16 (marca), D-28, D-29 |
| FN-02 | Android como plataforma principal, otimizado para aparelhos reais | brief §1/§11 | 🟡 Build IL2CPP/ARM64 configurada. Última medição em aparelho: X-63, 14/10. **Nenhuma build Android do commit atual** | D-08 |
| FN-03 | Futebol 3D com **visual realista** otimizado para Android | brief §11, Etapa 2 | ⬜ Só primitivas: nenhum modelo, animação, estádio ou áudio. Direção visual proposta (referência de transmissão estilo PES 2015, conteúdo original) em `docs/VISUAL_DIRECTION.md` (X-123) | **D-02** (origem da arte) |
| FN-04 | Totalmente offline | brief §10 | ✅ Nenhum acesso à rede no código | — |
| FN-05 | Partida 3D em tempo real com toda a lista do brief §4: movimento, bola, passe, chute, desarme, posicionamento, goleiro, placar, reinícios, câmera e IA | brief §4 | ✅ A1–A9. Lesão em campo 🟦 (X-100). Placar com poucos gols 🟡 (~0,7 gol em 6 min, meta de 2–4; X-84 (a)) | X-84 (a) |
| FN-06 | Duração configurável de **3, 4 ou 5 minutos** | brief §4, Etapa 2 | 🟡 Existe a configuração, mas com 4/6/10 min (`competitions.json`) | D-22 decidida em X-111: 3/4/5/6/10, padrão 4 (não implementada) |
| FN-07 | Controles padrão simples e **personalizáveis**: posição e tamanho dos botões, mapeamento e sensibilidade, em qualquer tamanho de tela | brief §5 | 🟡 Analógico + 4 botões + sprint com layout fixo (`InputAdapter.cs`) | D-26 |
| FN-08 | **Dificuldade ajustável** que muda de fato o comportamento da IA | brief §5, Etapa 2 | ⬜ Nível único (X-72, X-45). Os parâmetros da IA já estão em dados | D-23/a/b/c decididas em X-111, D-23d em X-112. Implementada na F1c (X-120) e validada no aparelho pelo usuário (X-121) |
| FN-09 | Carreira de treinador com equilíbrio entre gestão e partidas. Ciclo: preparar → jogar ou simular → avaliar → administrar → avançar | brief §2/§6 | 🟡 Ciclo completo nas regras (B1–B8, C1). Na UI: Hub, pré e pós-jogo, calendário e competições ✅ (C3a); elenco, escalação e tática 🟦 (C3b); mercado, clube, diretoria e finanças ⬜ (telas de espera) | — |
| FN-10 | **Criar o próprio clube** e **clubes editáveis** | brief §6/§7/§13, Etapa 2 | ⬜ Hoje só se escolhe um clube da Série D (X-93) | D-24 decidida em X-111 (substitui um da Série D); detalhes na F3 |
| FN-11 | Competições brasileiras da **Série D à Série A**, com estrutura configurável (clubes, formato, calendário, desempates, acesso e rebaixamento) | brief §7, Etapa 2 | 🟡 4 divisões × 16 clubes, pontos corridos, 4 sobem e 4 caem, desempates e calendário em dados ✅. Nomes "Liga Nacional — Divisão A–D" no código. Só pontos corridos | D-31, D-32 |
| FN-12 | Mercado de transferências **restrito ao Brasil** na v1: busca, ofertas, contrapropostas, valores, salários, contratos e orçamento. Jogadores gerados localmente | brief §9, Etapa 2 | 🟡 Regras e IA de mercado ✅ (B5). Faltam os comandos e as telas do usuário. Venda de jovens a "clubes estrangeiros fictícios" (`TransferWindow.SellYoungTalentAbroad`) | D-30 decidida em X-111 (só se contrata no Brasil; o exterior pode comprar); S-11 |
| FN-13 | **Treinamento** que evolui atributos e Overall, considerando posição, idade, potencial, frequência, intensidade, fadiga e recuperação, com limites | brief §8, Etapa 2 | ⬜ Só evolução passiva por idade, minutos, CT e auxiliar (`development.json`). OVR por posição ✅ | D-25 decidida em X-111 (híbrido); detalhes na F4 |
| FN-14 | Salvamento local robusto e recuperação de dados | brief §10, Etapa 2 | ✅ Formato JSON + gzip, versão de schema, migrações, checksum, escrita atômica, 3 backups e autosave (B8). A UI de recuperação ("save restaurado do backup") não está verificada | — |
| FN-15 | Níveis gráficos ajustáveis, medidos em aparelho real | brief §11 | ⬜ Nenhum uso de `QualitySettings`; a build usa URP Medium | D-27 (depende de D-02) |
| FN-16 | Economia, contratos e orçamento administráveis pelo usuário | brief §6/§9 | 🟡 Regras ✅ (B6). Decisões do clube do usuário automáticas | — |

### 2.1 Mantidos do MVP antigo e não citados pelo brief

Estes itens estão implementados e testados, mas o brief não fala deles. **Ficam na v1 (D-32, X-122 a X-124)**, com os limites de `DECISIONS.md` D-32a a D-32f:

- Copa Nacional (32 clubes, mata-mata em jogo único).
- Objetivos da diretoria, confiança e demissão.
- Reputação.
- Instalações: estádio e CT.
- Comissão técnica.
- Base com 3 jovens por ano.
- Notícias.

### 2.2 Critério de sucesso da v1 (brief §13)

A v1 integrada permite, **offline**:

1. Criar um clube.
2. Iniciar uma carreira.
3. Montar a equipe.
4. Jogar partidas 3D.
5. Treinar jogadores.
6. Fazer transferências nacionais.
7. Avançar por campeonatos e temporadas.

Ela é avaliada por diversão, clareza, consistência dos sistemas, desempenho em aparelho e facilidade de uso. **Nenhum item conta como concluído sem evidência de validação** (brief §12): teste automatizado executado e, para partida, UI e desempenho, validação no editor ou no aparelho.

### 2.3 Fora da v1

Continua valendo a lista "Fora do MVP" de `MVP_SCOPE.md`, **exceto**:

- **"Controles: layout editável"** e **"editor de clube"** entram na v1 (FN-07, FN-10).
- **"Modo treino"** entra, entendido como o treinamento da carreira (FN-13). Uma partida-treino fora da carreira continua fora.
- **"Dados reais licenciados, rostos realistas"** continua fora. O visual realista de FN-03 é sobre modelos, animação, iluminação e estádio, não sobre licenças. Se haverá rostos não genéricos fica dentro de D-02.

---

## 3. Contradições C-01 a C-14

Situação em 19/10/2026: o código e os documentos foram conferidos de novo nesta etapa e **todas as 14 continuam presentes no código**, porque nada foi implementado.

- **Resolvida (requisito):** a Etapa 2 ou o brief fixam o requisito. A implementação ainda não foi feita.
- **Parcial:** o requisito está fixado, mas falta um detalhe que só o usuário decide.

| # | Contradição | Ainda válida? | Situação | Resolução / o que falta |
| --- | --- | --- | --- | --- |
| C-01 | Duração 3/4/5 × 4/6/10 | Sim (`competitions.json`; calibração e metas de gols em 6 min) | **Parcial** | Requisito 3/4/5 fixado. Falta D-22: a duração padrão da carreira, o destino de 6 e 10 min e a meta de gols por duração |
| C-02 | Dificuldade que muda a IA × nível único | Sim (X-72, X-45) | **Parcial** | Requisito fixado; X-72 (1) e X-45 (1) substituídas. Falta D-23: quantos níveis, o que muda e se afeta a QuickSim |
| C-03 | Criar clube × só escolher | Sim (X-93) | **Parcial** | Requisito fixado; X-93 substituída e S-10 promovida. Falta D-24: substituir ou acrescentar, quais campos e quando editar |
| C-04 | Treinamento × sem treino | Sim (`development.json` passivo) | **Parcial** | Requisito fixado; exclusão "modo treino" do MVP_SCOPE substituída. Falta D-25: o modelo do treino |
| C-05 | Controles personalizáveis × layout fixo | Sim (`InputAdapter.cs`) | **Resolvida (requisito)** | Escopo do brief §5. Detalhe opcional em D-26 (o que "mapeamento" inclui, modo canhoto) |
| C-06 | Níveis gráficos × nada previsto | Sim | **Parcial** | Requisito do brief §11. Falta D-27: quais níveis e o que muda; depende da arte (D-02) |
| C-07 | Realista × low-poly estilizado | Sim (GAME_DESIGN §2) | **Resolvida (requisito)** | "Visual realista, otimizado para Android"; GAME_DESIGN §2 substituído. **D-02 (origem da arte) continua pendente e é o maior risco** |
| C-08 | Paleta nova × paleta antiga | Sim (`UiKit.cs`, HUD) | **Parcial** | As 4 cores do brief estão fixadas. Falta D-28: cores de estado, neutros escuros e modo escuro |
| C-09 | Menus modernos × "placar antigo + lambe-lambe" | Sim (GAME_DESIGN §2) | **Resolvida (requisito)** | Direção do brief §3. O logotipo e o ícone dependem de D-29 |
| C-10 | Mercado só no Brasil × venda de jovens ao exterior | Sim (`TransferWindow.SellYoungTalentAbroad`) | **Parcial** | Requisito fixado; GAME_DESIGN §1/§10 substituídos. Falta D-30: o que fazer com a venda ao exterior |
| C-11 | Formato configurável × só pontos corridos | Sim | **Pendente** | A Etapa 2 diz "Série D à Série A", mas não fala de formato. D-31: a v1 precisa de grupos ou mata-mata nas divisões? |
| C-12 | "Séries" × "Divisão"/"Liga Nacional" | Sim (`competitions.json`) | **Resolvida (requisito)** | Exibir "Série A–D". O nome da competição-mãe, hoje "Liga Nacional", fica em D-31 |
| C-13 | FOOTBALL NATION × ACESSO | Sim (`productName`, menus, `pt-BR.json`) | **Resolvida (requisito)** | Nome fixado. Falta a verificação de marca (D-16). A migração dos identificadores técnicos não é necessária agora |
| C-14 | Copa, diretoria, staff e instalações fora do brief | Não é contradição | **Resolvida** | Mantidos na v1 (D-32, X-122 a X-124) |

**Resumo:**

- **4 resolvidas no requisito:** C-05, C-07, C-09, C-12 (mais C-13, cuja marca ainda depende de D-16).
- **7 parciais, que precisam de um detalhe:** C-01, C-02, C-03, C-04, C-06, C-08, C-10.
- **2 pendentes:** C-11, C-14.
- **Nenhuma foi implementada.**

### 3.1 Atualização de 19/10/2026 (X-111)

A tabela acima fica como estava no registro da Etapa 2. Com as decisões do usuário em X-111, mudam de situação:

| # | Nova situação | Por quê |
| --- | --- | --- |
| C-01 | **Resolvida (decisão)** | D-22: 3/4/5/6/10, padrão 4, metas experimentais |
| C-02 | **Resolvida (decisão)** | D-23, D-23a, D-23b, D-23c (X-111) e D-23d (X-112): sem efeito na carreira |
| C-03 | **Resolvida (decisão); detalhes na F3** | D-24 |
| C-04 | **Resolvida (decisão); detalhes na F4** | D-25 |
| C-10 | **Resolvida (decisão); detalhes na F5** | D-30 |

**Continuam iguais:** C-06, C-08, C-11 e C-14.

**Nada disso está implementado:** as 14 continuam presentes no código.

---

## 4. Especificação por área

### 4.1 Partida

- **Design-alvo:** GAME_DESIGN Parte III (autoridade da partida) e as decisões X-12 a X-109 continuam valendo.
- **Durações (D-22, X-111):** 3, 4, 5, 6 e 10 min, **padrão 4**. A fadiga já é normalizada pela duração (X-12).
- **Metas de ritmo por duração (D-22b): parâmetros experimentais, não critérios rígidos.**
  - Partem do GAME_DESIGN §15 (2–4 gols no total e ~8–14 finalizações do usuário em 6 min), proporcionais ao tempo real.
  - Na F1a e na F1b vão para `Data/TestRanges/` como referência de teste.
  - Mudam com a calibração, com registro em `DECISIONS.md`.

  | Duração | Gols por partida (total) | Finalizações do usuário |
  | --- | --- | --- |
  | 3 min | 1,0–2,0 | 4–7 |
  | **4 min (padrão)** | **1,3–2,7** | **5–9** |
  | 5 min | 1,7–3,3 | 7–12 |
  | 6 min | 2,0–4,0 (GAME_DESIGN §15) | 8–14 |
  | 10 min | 3,3–6,7 | 13–23 |

- **Coerência com as regras de simulação:** a QuickSim sempre simula 90 minutos de jogo, nas faixas reais de `quicksim.json` (~2,6–2,7 gols), qualquer que seja a duração. A calibração (X-84 opção d, X-86) compara o volume em razão jogada/QuickSim, com a faixa `goalsPerMatch` de 0,18 a 1,3.
  - **Em 4 min, a meta experimental dá uma razão de ~0,5–1,0:** fica dentro da faixa atual, sem contradição.
  - **Em 10 min, o topo da meta (6,7) passaria do teto de 1,3** se a calibração rodasse nessa duração. Ela roda só num lote de duração fixa, então não há conflito hoje. Fica como observação para a F1b.
  - **Calibração durante a F1b (D-34, X-114):** o lote automatizado fica em **6 min**, com as tolerâncias de antes intactas, até o fim da F1b. Em 4 min, 9 métricas estão fora da faixa hoje (diagnóstico conhecido, não aprovado). A F1b só conclui com a calibração aprovada em **4 min**, a duração padrão, sem enfraquecer nenhum critério. Isso não muda a duração do jogo, que continua com padrão 4.
  - **Medido depois da F1b (X-115, 400 confrontos IA × IA, nível único):** 3 min 0,48 gol e 1,3 finalização por lado; **4 min 0,68 gol e 1,8 por lado**; 5 min 0,92 e 2,3; 6 min 1,10 e 2,9; 10 min 1,96 e 5,0. Antes da F1b, em 4 min: 0,42 gol e 1,3 por lado. As metas experimentais ainda estão acima disso.
  - **Depois da continuação da F1b (X-116, 1 200 confrontos, 6 sementes):** 4 min 0,68–0,75 gol e 1,8 finalização por lado; 6 min 1,11–1,14 gol e 2,9 por lado.
  - **Depois das decisões D-36/D-37/D-38 (X-117, mesmas condições):** 4 min 0,69–0,78 gol e 1,9 finalização por lado; 6 min 1,14–1,21 gol e 3,0 por lado. O domínio de bola erra mais nos times fracos (≈4,7% das recepções) que nos fortes (≤0,3%), também para o jogador do usuário.
  - **Limitação conhecida (S-07, X-118):** a mentalidade ofensiva é a mais eficiente em pontos por partida; a exposição defensiva de quem sobe o bloco ainda não está calibrada. Aceita temporariamente; melhoria futura.
- **Dificuldade (FN-08, D-23):**
  - **Níveis:** Fácil, Normal e Difícil. O Normal é recalibrado depois da F1b.
  - **Partida jogada:** muda decisão e reação da IA (GAME_DESIGN §24).
  - **Partida simulada:** muda só o cálculo do confronto do clube do usuário, nunca atributos permanentes nem outros confrontos. A calibração global roda sem dificuldade.
  - **Escolha:** em Configurações a qualquer momento, gravada no save.
  - **Implementação (F1c, X-120):** o nível age só sobre o adversário do usuário (decisão, reação, posicionamento e goleiro; na QuickSim, a força do adversário naquele confronto). Escolhido na nova carreira (padrão Normal) e mudado em Configurações. Em 4 min, o usuário vence 37% / 22% / 14,5% em Fácil / Normal / Difícil (IA × IA de referência).
- **Controles (FN-07):** a configuração padrão é a atual: analógico flutuante, 4 botões e sprint. A personalização é guardada **fora do save da carreira**, em configurações do aparelho, e uma opção restaura o padrão.

### 4.2 Carreira

- O que existe:
  - Ciclo da carreira por datas (X-90).
  - Plano do usuário: tática e escalação (X-97).
  - QuickSim calibrada com a partida jogada (C2).
- O que entra na v1:
  - **Comandos do usuário** para mercado, contratos, orçamento, obras e staff. Hoje essas decisões são automáticas para o clube do usuário (X-95).
  - Criação de clube (D-24).
  - Treinamento (D-25).
- **Regras que continuam:**
  - A carreira roda inteira sem Unity por 10 ou mais temporadas.
  - Os números de balanceamento ficam em JSON.
  - Quando o formato do save mudar, aumenta-se a versão do schema e cria-se uma migração.

### 4.3 Treinamento (FN-13)

O que já está fixado pelo brief §8:

- Desenvolve atributos individuais.
- O OVR continua derivado dos atributos por posição (`ovr.json`).
- Considera posição, idade, potencial, frequência, intensidade, fadiga e recuperação.
- O potencial é o teto. A evolução tem limites e precisa estar ligada ao treino.

O modelo de treino é **PENDENTE (D-25)**.

### 4.4 Mercado (FN-12)

Só clubes brasileiros do mundo do jogo. O que existe e o que falta:

- **Já existe nas regras:** valores, salários, negociação em duas etapas, contratos, agentes livres, reposição por jogadores gerados e IA de mercado (B5).
- **Entra na v1:**
  - Busca, ofertas e contrapropostas do usuário.
  - Telas de mercado e negociação.
  - Os números do scouting passam para os dados (S-11).
- **O destino da venda ao exterior** é PENDENTE (D-30).

### 4.5 Competições (FN-11)

- **Formato atual:** 4 séries de 16 clubes, 30 rodadas, 4 sobem e 4 caem.
- **Exibição:** passa a ser "Série A", "Série B", "Série C" e "Série D". A troca vem na etapa de identidade.
- **Configurável já hoje, em dados:** número de clubes, pontos, desempates, sobe/desce e calendário.
- **Pendentes:**
  - Outros formatos e o nome da competição-mãe (D-31).
  - ~~A permanência da Copa (D-32).~~ Decidida: fica na v1 no formato atual (32 clubes, cinco fases, jogo único; X-123).

### 4.6 Salvamento (FN-14)

O B8 continua. A v1 acrescenta dois requisitos:

- **Mensagem de recuperação:** quando um backup é restaurado, o usuário é avisado.
- **Saves antigos:** abrir um save de versão anterior depois de cada mudança de schema precisa funcionar, com fixture e teste.

### 4.7 Apresentação e desempenho (FN-01, FN-03, FN-15)

- **Visual realista**, condicionado ao desempenho:
  - **≥ 30 fps estáveis** no aparelho de entrada (Galaxy A07).
  - **Perto de 60 fps** no intermediário (Galaxy S10+).
  - Aparelhos e metas definidos em X-63 e TECHNICAL_SPEC §3.
- **A qualidade visual só conta como concluída quando medida em aparelho** (brief §11).
- **Níveis gráficos:** D-27.

---

## 5. Decisões substituídas

Todas foram substituídas por X-110 (19/10/2026). As entradas antigas continuam em `DECISIONS.md` com a marca "substituída".

| Antes | O que dizia | Agora |
| --- | --- | --- |
| X-72 (1) / D-17b | Dificuldade da partida: nível único | FN-08: dificuldade ajustável (detalhes em D-23) |
| X-45 (1) | Dificuldade da carreira: 1 nível fixo | FN-08: idem (D-23) |
| X-93 / D-18 | Só escolher um clube; criar é pós-MVP (S-10) | FN-10: criar clube e editar clubes na v1 (D-24) |
| D-16 (nome) | Nome de trabalho ACESSO | FOOTBALL NATION. A verificação de marca continua pendente |
| MVP_SCOPE "Duração 4/6/10 min" e GAME_DESIGN §14 "4, 6 ou 10 min (padrão 6)" | Durações | FN-06: 3/4/5 (D-22) |
| MVP_SCOPE "Fora do MVP: controles — layout editável" | Layout fixo | FN-07: personalizável |
| MVP_SCOPE "fora do escopo: modo treino, editor de clube" | Sem treino e sem editor | FN-13 e FN-10 |
| GAME_DESIGN §2 "3D low-poly estilizado, legibilidade > realismo" | Estilo da partida | FN-03: realista e otimizado. A legibilidade continua obrigatória |
| GAME_DESIGN §2 cores (#0F2A1D…), modo escuro padrão, arte de menus "placar antigo / lambe-lambe" | Identidade ACESSO | FN-01 / §1.1 (cores de estado e modo escuro: D-28) |
| GAME_DESIGN §1/§10 "venda de joias ao exterior" | Mercado com exterior | FN-12: só o Brasil (D-30) |
| GAME_DESIGN §6 "1 principal + 1–2 secundários" e "abaixo de 20 no fim de um turno" | Objetivos e avaliação da diretoria | D-32a (X-123, X-124): um objetivo principal por temporada, sem secundários; avaliação no fim da temporada |
| GAME_DESIGN §7 "Nível 1–5, salário, contrato" | Staff | D-32c (X-123, X-124): escolha direta do nível, sem candidatos nem contrato com duração |
| MVP_SCOPE e GAME_DESIGN §8 "requisito de estádio por divisão" | Requisito de estádio | D-32b (X-123): só aviso, sem bloquear o acesso |
| GAME_DESIGN §5 "Liga Nacional — Divisão A/B/C/D" | Nomes | "Série A–D" (competição-mãe: D-31) |

---

## 6. Decisões pendentes que dependem do usuário

Detalhes e opções em `DECISIONS.md` (seção Pendente).

### 6.1 Decididas pelo usuário em 19/10/2026 (X-111)

Estas decisões estão registradas, mas **não autorizam implementação**. Cada etapa (F1a, F1b, F1c, F3, F4, F5) continua precisando da autorização do usuário. Nenhum requisito FN-xx muda de estado com elas.

| Id | Decisão | O que ainda fica em aberto (não bloqueia a F1) |
| --- | --- | --- |
| D-22 / D-22b | Partidas de **3, 4, 5, 6 e 10 min, padrão 4**. Metas de gols e finalizações por duração são **parâmetros experimentais**, sujeitos a calibração (tabela em §4.1) | Os valores finais, depois da F1b |
| D-23 | Dificuldade **Fácil, Normal e Difícil** | Que parâmetros da IA cada nível muda (proposta técnica na F1c) |
| D-23a | O **Normal é recalibrado depois do ataque da IA** (F1b) | — |
| D-23b | A dificuldade vale nas partidas **jogadas e simuladas**. Na simulada, muda **só o cálculo do confronto do clube do usuário**, sem alterar atributos permanentes nem outros confrontos, nem a calibração global | — |
| D-23c | O nível pode ser **mudado em Configurações a qualquer momento** e fica **gravado no save** | — |
| D-23d (X-112) | Nesta etapa a dificuldade afeta **só as partidas jogadas e simuladas** (D-23b). Não altera diretoria, finanças, recompensas, treinamento, evolução permanente nem progressão da carreira | Efeitos na carreira só com nova decisão |
| D-24 | O clube criado **substitui um participante da Série D**, mantendo o número de participantes | Qual clube sai, campos editáveis, elenco inicial, edição de clubes existentes (F3) |
| D-25 | **Treinamento híbrido:** exercícios escolhidos pelo usuário, evolução calculada pelo jogo | Catálogo de exercícios, intensidade, energia e lesão (F4) |
| D-30 | **Clubes estrangeiros podem fazer propostas por jogadores brasileiros. As contratações continuam só no mercado brasileiro.** Tudo funciona offline | Se a IA dos clubes brasileiros também vende ao exterior sozinha, como hoje; lista dos clubes estrangeiros fictícios (F5) |
| X-94b | **Uma única linha de trabalho faz a F1.** A sessão auxiliar não desenvolve o ataque da IA ao mesmo tempo | — |

**Contradições resolvidas por estas decisões:**

- **D-30 × FN-12:** o requisito passa a ser lido como "contratações só no Brasil". As vendas ao exterior são permitidas.
- **D-23b × GAME_DESIGN §24** ("dificuldade altera decisão, nunca atributos"): a regra fica lida como "nunca atributos permanentes". Na partida jogada a dificuldade mexe em decisão e reação da IA. Na simulada, mexe só no cálculo daquele confronto.

### 6.2 Ainda pendentes

| Id | Assunto | Bloqueia |
| --- | --- | --- |
| D-26 | Escopo do "mapeamento" de controles, modo canhoto, vibração | Parte de F6 |
| D-27 | Níveis gráficos | F7c |
| D-28 | Cores de estado, neutros escuros, modo escuro | Parte de F7a |
| D-29 | Logotipo, escudo do produto e ícone: quem produz | F7a (ícone final), F8 |
| D-31 | Formato dos campeonatos na v1 e nome da competição-mãe | Não bloqueia F1–F6 |
| D-32 | Copa, diretoria, instalações e staff continuam na v1? **Sim (X-122)**. Decidida em X-123 e X-124 (detalhes em `DECISIONS.md` D-32a a D-32f) | Resolvida para a F2a; o destino da carreira encerrada ao iniciar outra fica para a F2b |
| D-33 | Ambiente de testes: instalar o runtime .NET 8 ou migrar os testes para `net10.0` | Não bloqueia (roll-forward funciona) |
| P-125 | Penalidade de confiança por 6 meses de caixa negativo (GAME_DESIGN §11): **continua pendente** (X-126); não entra na v1 por enquanto, sem exclusão permanente | Não bloqueia; reavaliar antes de mudar a confiança por caixa negativo |
| D-02 | Origem dos modelos 3D e animações (já pendente), agora com o requisito "realista" | F7b |
| D-16 | Verificação da marca FOOTBALL NATION (INPI e lojas) | F8 |
| D-07, D-08 | Monetização; Android mínimo | F8 |
| X-84 (a) | Ataque da IA: tarefa já aprovada, executada pela linha única da F1 (X-94b). Só falta autorizar a F1 | F1b |
| X-85 (b) | Mando de campo na partida jogada | Não bloqueia |
