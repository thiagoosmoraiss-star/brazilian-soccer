# TEST_PLAN.md

Fonte: Especificação Técnica §17–18. Faixas numéricas de aceitação (ex.: média de gols) ficam em arquivos de dados de teste versionados, não no código dos testes, e partem das metas do design (ex.: ~2–4 gols por partida de 6 min). Quando uma faixa ainda não foi definida, o teste registra a métrica e a faixa fica **PENDENTE** até a etapa correspondente.

## 1. Execução

| Categoria | Ferramenta | Quando roda |
| --- | --- | --- |
| Rápidos (Unit, Gameplay de cenário, Save, Determinismo) | `dotnet test` (zona pura) | Todo commit |
| Lentos (Simulation, Headless, Career, Calibração) | `dotnet test` com categoria lenta | Diário e antes de merge |
| PlayMode | Unity Test Framework em batchmode | Antes de build |
| Performance | Build de desenvolvimento no aparelho de referência + Profiler | A cada marco (A7, C1, C3, C5) |

Regras: todo teste aleatório recebe semente explícita e a registra ao falhar; nenhum teste depende de relógio real; testes lentos têm tempo máximo.

## 2. Tipos de teste

### Unit
Funções puras: RNG, curvas e `Balance.Eval`, validador de dados, OVR, geometria do campo, integração da bola, movimento, fadiga, regras de falta/cartão, cálculo de valor/salário, livro-caixa, desempates, migrações.

### Gameplay (cenários no MatchEngine headless)
Situações montadas com posições e atributos fixos:
- passe de 20 m sem pressão chega ao alvo;
- erro de passe cresce com pressão, pé ruim, orientação e distância;
- chute do meio-campo raramente é preciso; chute na área com Finalização alta acerta mais;
- desarme só acontece com bola alcançável;
- troca automática não ocorre durante contenção/carrinho/disputa; histerese impede alternância;
- goleiro defende chute fraco central; não alcança fora do alcance;
- impedimento marcado no momento do passe;
- bola parada sempre executa e termina.

### Física
- previsão de trajetória = integração dentro de tolerância;
- energia da bola nunca aumenta sem impulso;
- bola nunca atravessa trave/travessão (teste por segmento, incluindo chute forte);
- gol só com cruzamento completo da linha entre postes e abaixo do travessão;
- saída detectada só com cruzamento completo da linha;
- quique converge para rolando.

### Atributos
- validação de dados: todo atributo alimenta ao menos um `Effect`;
- sensibilidade: para cada atributo, time com +15 nele vs clone → diferença estatisticamente significativa na métrica associada (headless e QuickSim);
- monotonicidade: curvas declaradas monótonas não invertem;
- regra de percepção: diferença de 5 pontos pequena, 15+ clara (faixas em dados).

### IA
- sem oscilação de intenção (mesmo jogador alternando alvos em ciclos curtos);
- sem "bolo" (limite de jogadores do mesmo time perto da bola fora de disputa);
- compactação dentro dos limites por fase;
- partida headless sempre termina;
- Posicionamento baixo → maior erro médio de alvo;
- mudança tática altera posições médias na direção esperada.

### Headless Match
Lotes de 100–1.000 partidas no MatchEngine sem render: gols, 0×0, finalizações, escanteios, faltas, cartões, posse, curva diferença de OVR → vitória/empate/derrota, zebras existem.

### Simulation (QuickSim)
Lotes de 1.000–10.000 partidas: mesmas métricas do headless; nenhum resultado impossível; estatísticas coerentes com eventos; tempo por partida dentro do limite.

### Divergência QuickSim × Headless (calibração)
Mesmos confrontos nos dois motores; distribuições comparadas com tolerâncias em dados: gols por partida, % de 0×0, finalizações, finalizações no alvo, escanteios, faltas, cartões, posse por diferença de força, curva OVR → resultado, sensibilidade por atributo (mesma direção). Falha bloqueia merge a partir de C2.

### Career
10–30 temporadas completas, várias sementes, sem interação humana, sem Unity.

### Calendário
Todos os clubes jogam todas as partidas; nenhum clube com 2 jogos na mesma data; intervalo mínimo respeitado; janelas nas datas previstas.

### Promoção/rebaixamento
Nº de clubes por divisão constante; quem sobe/cai bate com a tabela final e as regras (D-13).

### Progressão
Jovens de potencial alto e minutos atingem a faixa esperada até 24 anos; ninguém ultrapassa o potencial; veteranos caem; D→A em 6–10 temporadas com política automática razoável.

### Economia
Saldo = soma do livro-caixa sempre; nenhum caixa infinito; falência sistemática abaixo do limite; clube médio perto de zero; acesso gera superávit; gastar tudo em salário sem subir gera déficit.

### Mercado
Elencos entre 20–26; valor médio e salário médio por divisão sem inflação acima do limite por temporada; nenhum jogador com dois contratos ativos; negociações sempre terminam; propostas ao jogador limitadas por janela.

### Save
Ida e volta idêntica; migração de cada fixture de versão anterior; corrupção simulada → recupera backup; escrita interrompida não destrói o save válido; snapshot de partida restaura estado idêntico.

### Determinismo
Mesma semente + mesmas entradas → `MatchResult` e `CareerState` idênticos; alterar um sistema não muda a sequência aleatória de outro (fluxos independentes); replay por entradas gravadas reproduz a partida.

### PlayMode
Cena Match com input gravado; navegação da Shell; retomar após pausa do app.

### Performance
No aparelho de entrada e no intermediário de referência (D-03): FPS da partida (meta 60, mínimo 30 estável), ausência de picos de GC no loop, memória, tempo de carregamento, tempo do Avançar. Cenários fixos repetíveis.

## 3. Invariantes

Verificados em testes e, nas builds de teste, a cada passo/data:

| Invariante | Onde |
| --- | --- |
| Nenhum placar impossível (negativo, acima do limite) | Partida, QuickSim |
| Estatísticas coerentes com eventos (gols = eventos de gol) | Partida, QuickSim |
| Posse só muda por evento válido (passe, desarme, interceptação, bola solta, saída, reinício) | MatchEngine |
| Bola sempre dentro do volume do campo + margem | MatchEngine |
| 11 ou menos jogadores por time em campo; substituições dentro do limite | MatchEngine |
| Atributos 1–99; energia 0–100 | Todos |
| Atributos produzem efeitos na direção declarada | Balance |
| Sem loops de IA | MatchEngine |
| Todo Id referenciado existe | Save, Career |
| Jogador com no máximo um contrato ativo | Career |
| Saldo = soma dos lançamentos | Economy |
| Calendário consistente | Career |
| Nº de clubes por divisão constante | Career |
| Ninguém acima do potencial | Career |
| Determinismo por semente | Todos os puros |
