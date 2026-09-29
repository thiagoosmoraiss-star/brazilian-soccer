# GAME_DESIGN.md

Fontes: GDD (visão, carreira, identidade) e "Partida — design aprofundado" (autoridade para tudo da partida). Todos os números são **valores iniciais de balanceamento**, a validar em teste, salvo quando marcados como regra. O que está no MVP é definido em `MVP_SCOPE.md`; este documento descreve o design-alvo.

---

## Parte I — Produto

### 1. Visão e proposta

Jogo de futebol mobile centrado em uma carreira de clube brasileiro fictício do interior, que sobe da Divisão D ao topo. Diferencial: o futebol brasileiro como estrutura (pirâmide A/B/C/D com acesso e rebaixamento, copa nacional como atalho financeiro, venda de joias ao exterior, pressão de diretoria e torcida). Partida jogável em que a qualidade do elenco é visível e a execução do jogador importa.

Sem online, sem pay-to-win, sem dados reais licenciados.

### 2. Identidade

- **Nome de trabalho:** ACESSO (PENDENTE: checagem de marca e lojas).
- **Tom:** brasileiro, bem-humorado e seco; notícias em linguagem de rádio/jornal local, sem caricatura.
- **Arte dos menus:** placar de estádio antigo + cartaz lambe-lambe + jornal esportivo; papel texturizado, faixas diagonais, carimbos.
- **Partida:** 3D low-poly estilizado, camisas em cor sólida, número legível. Legibilidade > realismo.
- **Escudos:** gerados por templates (formas + 2 cores + símbolo genérico).
- **Rostos:** sem rostos realistas no MVP; avatar em silhueta.
- **Cores (propostas):** base #0F2A1D, superfície #F3EFE4, destaque #F5C518, positivo #2FA84F, negativo #D7263D, neutro #8A8F87. Modo escuro padrão.
- **Tipografia:** display condensada (Bebas Neue ou Barlow Condensed); UI Inter ou Barlow com números tabulares. Licença OFL a confirmar no download.
- **Monetização:** PENDENTE (hipótese do GDD: grátis com anúncio recompensado opcional + compra única para remover anúncios; nunca pay-to-win).

---

## Parte II — Carreira

### 3. Início

1. Escolher clube entre 6–8 opções da Divisão D com perfis diferentes (ou criar clube por templates — ver `MVP_SCOPE.md`).
2. Escolher dificuldade da carreira (orçamento e paciência da diretoria) — PENDENTE: se entra no MVP.
3. Elenco inicial gerado: 22 jogadores, média 45–55, 2–3 jovens promissores, 1–2 veteranos.
4. Diretoria apresenta objetivos.

Um save = um clube. A carreira só termina com demissão (assumir outro clube: pós-MVP).

### 4. Temporada e calendário

- Temporada = ano civil. Calendário gerado por dados em "datas" (rodadas).
- Janelas de transferência: janeiro e julho. Agentes livres e renovações a qualquer momento.
- Dezembro: premiações, acesso/rebaixamento, fim de contratos, aposentadorias, evolução anual, balanço, novos objetivos.
- Entre partidas: "dia de gestão"; **Avançar** leva ao próximo compromisso que exige o jogador.
- Estaduais existem no design-alvo, fora do MVP.

### 5. Futebol brasileiro fictício

Nomenclatura própria (ex.: "Liga Nacional — Divisão A/B/C/D", "Copa Nacional", "Estadual"), sem marcas reais. Clubes fictícios com UF real.

**Design-alvo (GDD):**

| Competição | Clubes | Formato |
| --- | --- | --- |
| Divisão A | 20 | Pontos corridos, 38 rodadas; 4 caem |
| Divisão B | 20 | Pontos corridos; 4 sobem, 4 caem |
| Divisão C | 20 | 1ª fase + quadrangulares de acesso |
| Divisão D | 32–64 | Grupos regionais + mata-mata |
| Copa Nacional | 64 | Mata-mata |
| Estaduais | 12–16 | Curto + finais |
| Continental | 32 | Pós-lançamento |

**MVP:** 4 divisões de 16 clubes em pontos corridos (30 rodadas) + Copa Nacional mata-mata em jogo único com 32 clubes. PENDENTE: número de clubes que sobem/caem com 16 clubes, critério de classificação para a Copa, tamanho total do mundo no MVP.

Regras de classificação (parametrizáveis): vitória 3, empate 1; desempate por vitórias, saldo, gols pró, confronto direto, cartões, sorteio. Mata-mata empatado → pênaltis.

### 6. Objetivos, diretoria e reputação

- Objetivos de temporada: 1 principal + 1–2 secundários (subir, não cair, fase da copa, fechar no azul, usar jovens).
- Objetivos de longo prazo: pós-MVP.
- **Confiança da diretoria** 0–100; abaixo de 20 no fim de um turno → demissão (tolerância pela dificuldade).
- **Reputação do clube** (1–5 estrelas; interno 0–1000): patrocínio, público, quem aceita vir, prêmios. **Reputação do treinador:** negociações; propostas de outros clubes pós-MVP.

### 7. Comissão técnica

Nível 1–5, salário, contrato. MVP (conforme GDD §2): preparador físico (energia, lesões), auxiliar técnico (evolução), olheiro (scouting). Demais funções pós-MVP.

### 8. Instalações

| Instalação | Níveis | Efeito | MVP |
| --- | --- | --- | --- |
| Estádio | 1–10 | Capacidade, bilheteria, requisito por divisão | Sim |
| Centro de treinamento | 1–5 | Evolução | Sim |
| Departamento médico | 1–5 | Lesões | Não |
| Categoria de base | 1–5 | Jovens | Não (base gera 3 jovens/ano fixos) |
| Loja/marketing | 1–3 | Produtos | Não |

Obras com custo, tempo (datas) e manutenção anual.

### 9. Jogadores

- **18 atributos (1–99):** Velocidade, Agilidade, Resistência, Força, Passe, Cruzamento, Drible, Finalização, Chute de longe, Cabeceio, Desarme, Visão, Posicionamento, Compostura, Reflexo (GK), Posicionamento GK, Jogo aéreo (GK), Mãos (GK).
- Dados fixos: altura, pé dominante, pé ruim 1–5, nacionalidade, nascimento.
- **Posições:** GOL, ZAG, LD, LE, VOL, MC, MEI, PD, PE, ATA; principal + até 2 secundárias. Fora de posição −10% OVR efetivo; secundária −3%.
- **OVR** por posição: média ponderada (pesos em dados).
- **Potencial** 40–99 oculto; visto em faixa conforme olheiro.
- **Curva de idade:** 16–21 rápido; 22–26 lento; 27–30 estável; 31–33 queda leve (físico primeiro); 34+ queda acentuada, aposentadoria.
- **Evolução semanal** = f(idade, distância ao potencial, minutos, CT, auxiliar, notas); ajuste anual em dezembro.
- **Moral** 5 níveis (±5% em jogo); **forma** = média das últimas 5 notas; **energia** 0–100%.
- **Lesões:** leve (1–2 datas), média (3–6), grave (2–6 meses).
- **Suspensões:** 3 amarelos = 1 jogo; vermelho = 1–3 jogos; por competição.
- **Traits:** até 3 por jogador, com efeito mecânico (lista de exemplos no GDD). PENDENTE: quais traits entram no MVP (o GDD cita "no máximo 4 no MVP"; o design da partida deixa traits em campo para depois).

### 10. Mercado e contratos

- Negociação em duas etapas: clube vendedor (valor) → jogador (salário, anos, status prometido). Até 3 rodadas; ofertas baixas encerram.
- Disposição do jogador = reputação relativa, divisão, salário, tempo de jogo. Craques recusam clubes pequenos.
- Vendedor aceita se oferta ≥ valor × fator (importância, contrato, finanças).
- Propostas da IA ao jogador; clubes estrangeiros fictícios compram jovens de alto potencial.
- Renovação a partir do último ano; pré-contrato nos últimos 6 meses.
- Scouting com faixas de atributo que estreitam.
- Empréstimos, cláusulas, parcelas, trocas: pós-MVP.
- IA de mercado: clubes mantêm 20–26 jogadores; lacunas completadas com jogadores gerados.

### 11. Economia

- Receitas: TV (por divisão), bilheteria, patrocínio, premiações, transferências, produtos (pós-MVP).
- Despesas: salários (teto recomendado ~60–70% da receita), staff, manutenção, transferências, obras.
- Orçamento no início da temporada (teto salarial + verba de transferências).
- Caixa negativo: 1º mês alerta; 3 meses bloqueio de contratações; 6 meses queda forte de confiança. Sem empréstimo bancário.
- Escala ~10× entre D e A. Premiação relativa: D=1, C=3, B=8, A=40, Copa=25.
- Meta de balanceamento: clube médio fecha o ano perto de zero; acesso gera superávit.

### 12. Progressão

| Etapa | Temporadas | OVR elenco | Problema dominante |
| --- | --- | --- | --- |
| D | 1–2 | 45–55 | Pouco dinheiro, elenco fraco |
| C | 1–2 | 55–62 | Acesso incerto, estádio pequeno |
| B | 2–3 | 62–70 | Maratona + copa |
| A (sobreviver) | 1–2 | 70–75 | Risco de queda |
| A (brigar) | 2+ | 75–85 | Expectativa, assédio aos craques |

Freios: recusa de jogadores por reputação, estádio mínimo por divisão, propostas pelos destaques, expectativa crescente da diretoria.

### 13. UI

Menus em retrato, partida em paisagem. Hub com botão dominante **Próximo jogo / Avançar**; tudo a ≤ 2 toques. Barra inferior: Clube · Elenco · Mercado · Competições · Diretoria. Alvos de toque ≥ 44–48 pt. Números com variação ▲▼. Telas: tela inicial, hub, elenco/escalação, perfil, mercado, negociação, calendário, competições, pré-jogo, partida, pausa, pós-jogo, finanças, clube, diretoria, configurações. Meta: fim de partida → próxima em ≤ 3 toques.

---

## Parte III — Partida (autoridade: "Partida — design aprofundado")

### 14. Modos

| Modo | Descrição | MVP |
| --- | --- | --- |
| Jogar | Controle total | Sim |
| Assistir | IA nos dois times, só táticas | Não |
| Simular | Resultado pela QuickSim | Sim |

Simular rende o mesmo que jogar (sem penalidade). Duração real configurável 4, 6 ou 10 min (padrão 6); relógio acelerado a 90 min + acréscimos.

### 15. Sensação alvo

"Eu joguei bem e meu time é limitado" deve ser possível. Responsiva (reação em 1 passo, buffer de entrada), fácil de aprender (tocar já funciona), difícil de dominar (força manual, passe fora do cone, finta, colocado), sessões curtas, craque perceptível em um lance, decisões táticas visíveis. Nunca: gol roteirizado/rubber-banding, animação que tira o controle, troca automática que causa gol. Ritmo alvo em 6 min: ~8–14 finalizações do jogador, ~2–4 gols no total.

### 16. Câmera

Broadcast lateral elevada (~30–45°), dinâmica, paisagem: segue a bola com amortecimento e lookahead, zoom dinâmico (afasta no meio, aproxima na área e bola parada), ajuste de inclinação na lateral distante, time do jogador sempre atacando da esquerda para a direita, radar translúcido recolhível, setas para companheiros fora da tela. Presets Padrão/Alta/Próxima (MVP: só Padrão).

### 17. Controles

Analógico flutuante à esquerda; 4 botões + sprint à direita, uma função por fase:

| Posição | Ataque | Defesa |
| --- | --- | --- |
| Grande (base) | Chute | Carrinho |
| Esquerda do grande | Passe | Trocar jogador |
| Acima do grande | Enfiada | Contenção (segurar) |
| Diagonal superior | Alto (lançamento/cruzamento) | Pressionar com 2º defensor (segurar) |
| Borda | Sprint (segurar) | Sprint (segurar) |

- Troca de ícones em 150 ms; botão pressionado na troca executa a função da fase em que foi pressionado.
- Memória de sprint: 0,4 s após soltar se o analógico continua empurrado.
- Buffer de entrada ~150 ms.
- Layout editável e modo canhoto (pós-MVP).

**Seleção:** com bola, controla o portador; ao passar, controle vai ao recebedor quando a bola sai do pé. Sem bola, troca automática pelo menor "tempo até interceptar" ponderado por posição. Gatilhos: perda de posse, passe adversário, jogador batido/2 m atrás, bola solta. Anti-erro: histerese (~25% melhor), trava de intenção, trava pós-manual 1 s, nunca durante carrinho/contenção/disputa, anel indicando o próximo. Troca manual por toque; direcional por arraste (pós-MVP). Assistência Completa/Semi/Manual (MVP: Semi).

### 18. Movimento e fadiga

| Parâmetro | Valor inicial | Atributo |
| --- | --- | --- |
| Trote | 60–65% da máxima | Velocidade |
| Sprint máx. | 7,0–9,5 m/s | Velocidade |
| 0 → máx. | 1,3–0,7 s | Agilidade |
| Parar | 0,3–0,5 s | Agilidade |
| Com bola | −10% (curta) a −5% (longa) | Drible |

Curvas: até 45° sem perda; 45–90° perde 15–30%; >90° perde 40–60% e 0,2–0,4 s. Sprint aumenta raio de curva e toque.

Fadiga: sprint gasta 5–6× o trote; Resistência reduz até 40%; <60% perde até −12% de velocidade/aceleração; <40% até −10% de precisão e mais risco de lesão; +15–20 no intervalo. **Gasto normalizado pela duração configurada** (decisão da Especificação Técnica): taxas definidas para 6 min, multiplicadas por 6/duração, para que o gasto total por partida seja igual em 4, 6 ou 10 min.

### 19. Passes

Regra: toque = o jogo resolve a força; segurar = você resolve. Direção pelo analógico.

| Tipo | Entrada |
| --- | --- |
| Curto (até ~25 m) | Passe (toque) |
| Longo rasteiro | Passe (segurar) |
| Enfiada | Enfiada (toque) |
| Enfiada longa/aérea (>70%) | Enfiada (segurar) |
| Lançamento | Alto fora da zona de cruzamento |
| Cruzamento alto | Alto na zona de cruzamento |
| Cruzamento rasteiro/recuo | Passe (segurar) na zona de cruzamento |
| De primeira | Qualquer passe com bola chegando (buffer) |

Alvo por cone: Completa ±45°, Semi ±25°, Manual sem trava. Sem ninguém no cone, vai ao espaço. Erro = produto de fatores: Passe/Cruzamento (Passe 90 erra ~¼ do Passe 40), Visão (enfiadas), pressão < 2 m (+30–80%, reduzida por Compostura), orientação do corpo (+0–60%), pé ruim (+0–50%), de primeira (+25%), distância, energia < 40% (até +10%). Passe alto dá bola mais veloz e limpa. Domínio usa Drible e Compostura. O sistema ajuda a executar a escolha, nunca a corrige.

### 20. Finalização

Segurar e soltar Chute: força pelo tempo (barra ~0,8 s; ideal 40–75%; acima cresce erro vertical); canto pelo analógico ao soltar; neutro = canto escolhido pela assistência. Sem cursor no gol. Colocado: deslizar para cima saindo do botão antes de soltar. Cabeceio: Chute = ao gol, Passe = para companheiro; disputa por posição > altura + Força + Cabeceio. Fatores: Finalização (área), Chute de longe (fora), pressão < 1,5 m (+40–100% erro, −10% força), bloqueio, pé ruim (+0–60% erro, −10–25% força), equilíbrio, de primeira (+20% erro, menos reação do goleiro), energia.

### 21. Drible

Sem botões de habilidade. Condução (Drible define intervalo de toques), corte (Agilidade + Drible), finta de corpo (pulso < 0,25 s), arrancada (sprint, toque longo), proteção (soltar/apontar para trás, Força), parar. 1×1 sem dado: desarme só acerta se alcança a bola exposta. Dribles especiais pós-MVP.

### 22. Defesa

Contenção (segurar), desarme em pé automático com bola exposta a < ~1 m, pressionar com 2º defensor (segurar), carrinho (2–3 m; falta se contato antes da bola; por trás cartão provável; erro = 1 s no chão), interceptação automática na linha, disputa de corpo. Companheiros não dão bote sozinhos (só com Pressionar ou tática de pressão). Falta/cartão por atraso, ângulo, velocidade, oportunidade clara de gol e rigor do árbitro.

### 23. Goleiro

IA (manual só em pênalti). Bissetriz do ângulo; avança/recua com a bola; líbero conforme a linha. Reação 0,15–0,30 s (Reflexo) + penalidades; alcance limitado, sem teletransporte. Encaixa/espalma/rebate (Mãos, força e efeito). Sai no 1×1 com bola exposta; sai na enfiada só se chega antes. Cruzamentos na pequena área/alcance (Jogo aéreo). Rebotes plausíveis; levanta em 0,6–1,0 s. Erros raros e explicáveis (~1 visível a cada 3–4 jogos para goleiro médio). Nunca: ignorar chute fraco central, pular errado sem finta, andar para dentro do gol.

### 24. IA

Três camadas: **time** (fases: ataque organizado, construção, transição ofensiva 2–4 s, transição defensiva 2–3 s, defesa organizada), **função** (posição-base deslocada pela bola ~40–60% lateral, fase, tática, compactação 10–15 m defendendo / 20–25 m atacando, ~35 m entre defesa e ataque, impedimento) e **indivíduo** (apoio, ruptura, ocupação de espaço, área em cruzamento; zona com entrega, cobertura, linha junta, marcação na área; decisão com bola por valor da ação). Dificuldade altera decisão, nunca atributos. Nenhum jogador parado olhando a bola; nenhum "bolo".

### 25. Física

Determinística e própria; bola com fórmulas previsíveis (rolando com atrito constante, parábola com arrasto leve, efeito como desvio lateral, quique perdendo 40–55% vertical e ~15% horizontal, trave com reflexão, rede absorve); jogadores como círculos ~0,4 m; contato por regra; passo fixo 50–60 Hz; animação segue o movimento.

### 26. Atributos em jogo

Cada atributo tem efeito visível (tabela completa em `TECHNICAL_SPEC.md` §8). Regra de percepção: 5 pontos quase imperceptíveis em um lance; 15+ óbvios. Modificadores: energia, moral/forma (±5%), fora de posição (−10%).

### 27. Táticas

Formações 4-4-2, 4-3-3, 4-2-3-1 (MVP); 3-5-2, 5-3-2, 4-1-4-1, 4-4-1-1 depois. Mentalidade (5), linha (3), pressão (3) no MVP; largura, laterais, presets rápidos e avisos do auxiliar depois. Substituições na pausa: 5 em 3 paradas + intervalo (parametrizável).

### 28. Bola parada

Padrão único: mirar com analógico, força por barra, botão define o tipo.

| Lance | MVP |
| --- | --- |
| Lateral | Passe = companheiro livre na direção; Alto = longo |
| Escanteio | Região pelo analógico; Alto = cruzamento; Passe = curto |
| Falta indireta | Como passe |
| Falta direta (até ~30 m) | Mira + força; **sem curva no MVP**; barreira automática |
| Pênalti batendo | Canto/altura + força; Compostura reduz oscilação |
| Pênalti defendendo | Escolher lado no chute |
| Tiro de meta | **PENDENTE** (proposta: Passe = curta, Alto = longa) |

### 29. Regras da partida

Faltas, cartões (2 amarelos = expulsão), impedimento verificado no momento do passe (sem VAR), pênalti, escanteio, lateral, tiro de meta, lesão em jogo com substituição forçada. Suspensões levadas à carreira.
