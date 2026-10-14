# REFERENCES.md — dados reais e referências externas

Referência de dados reais para calibração. Origem: especificação enviada pelo usuário em 14/10/2026 (dados reais da partida e da carreira + referências visuais de um vídeo), conferida pelo Claude Code com pesquisa na web. **Os números do jogo continuam vivendo em `Data/`**; este arquivo só registra de onde vêm as metas e o quanto confiar em cada uma.

Legenda de confiança: **Confirmado** (fonte encontrada) · **Plausível** (coerente com o que se conhece, sem fonte primária encontrada) · **Corrigido** (a especificação divergia da fonte) · **Não verificado**.

## Partida (por jogo, futebol real)

| Métrica | Especificação | Situação | Nota / fonte |
| --- | --- | --- | --- |
| Faltas | — | Confirmado | ~24,6 por jogo, 2ª liga mais faltosa (levantamento CIES citado pelo [Soccerway](https://br.soccerway.com/noticias/futebol-brasileirao-betano-brasileirao-e-2-liga-mais-faltosa-do-planeta-veja-ranking/baILS0sK)); flashscore 25,2–28,5 |
| Vermelhos | 0,25 | Corrigido | Brasileirão 2023-24: 0,49 vermelho e 3,53 amarelos por jogo (ESPN, citado pelo [Jornal de Brasília](https://jornaldebrasilia.com.br/mais-esportes/qual-a-media-de-cartoes-no-campeonato-brasileiro-analise-estatistica-dos-cartoes-amarelos-e-vermelhos-na-temporada/)); por árbitro em 2025, todos abaixo de 0,35 ([Itatiaia](https://www.itatiaia.com.br/bets/guias/media-de-cartoes-arbitros-brasileirao/)). Faixa real: 0,25–0,5 |
| Finalizações (total) | 24–27 | Não verificado | sem média de liga encontrada |
| No alvo | 8–10 | Não verificado | idem |
| Posse do dominante | 55–65% | Plausível | |
| Escanteios | 9–11 | Plausível | Flamengo 2025: 6,7 por jogo só do time, líder da liga ([Flamengo](https://en.flamengo.com.br/noticias/futebol/flamengo-lidera-a-tabela-e-as-estatisticas-do-brasileirao-2025)) |
| Impedimentos | 3–4 | Plausível | sem média de liga encontrada; no jogo, meta escalada ~1,2–1,6 (X-67), hoje ~0,7 (X-68) |
| Pênaltis | 0,30 | Plausível | |
| Conversão de pênalti | 76–79% | Plausível | |
| Gols | — | Confirmado | Série A 2024: 2,44 (placar.com.br) |

## Lesões

| Métrica | Especificação | Situação | Nota |
| --- | --- | --- | --- |
| Lesões por jogo (dois times) | 0,8–1,2 | Plausível | coerente com o estudo de lesões de clubes da UEFA (ECIS: lesões com afastamento por 1000 h de jogo; [UEFA/BJSM](https://bjsm.bmj.com/content/55/19/1084)); o estudo usa "carga de lesão" e não as faixas abaixo |
| Gravidade leve (1–7 d) / média (8–28 d) / grave (28+ d) | 50 / 35 / 15% | Plausível | proporções na linha da literatura de Ekstrand et al. (lembrança, sem tabela primária encontrada nesta pesquisa) |

## Física e goleiro

| Métrica | Especificação | Situação | Nota |
| --- | --- | --- | --- |
| Sprint | 33–37 km/h | Plausível | `effects.json` SprintSpeed: 7,0–9,5 m/s = 25–34 km/h (topo um pouco abaixo) |
| Aceleração | 6–7 m/s² | Não verificado | estudos de GPS em jogo medem picos bem menores (~2–5 m/s²; [PMC9649751](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC9649751/)); 6–7 é aceleração teórica máxima de perfil A–S |
| Velocidade do chute | 90–120 km/h | Confirmado no jogo | ShotPowerMax 22–34 m/s = 79–122 km/h |
| Passe | 30–50 km/h | Plausível | |
| Reação do goleiro (sensação de jogo) | 0,15–0,25 s | Já no jogo | GkReactionTime 0,15–0,30 s (GAME_DESIGN §23) |
| Alcance / duração do mergulho | 2,5–3,0 m / 0,6–0,8 s | Plausível | |
| Distância percorrida | 9–12 km | Plausível | |
| Queda nos 15 min finais | 15–20% | Plausível | |
| Efeito Magnus (curva) | requisito | Fora do MVP | MVP_SCOPE: falta direta sem curva; ROADMAP A8 "Não implementar: curva em falta" |

## Carreira (Brasil real)

| Métrica | Especificação | Situação | Nota / fonte |
| --- | --- | --- | --- |
| Série D | 64 clubes, grupos + mata-mata, 4 sobem | **Corrigido** | a partir de 2026: **96 clubes**, 16 grupos de 6 (ida e volta), mata-mata; semifinalistas sobem + playoff dos eliminados nas quartas por 2 vagas = **6 sobem**; 24 datas ([TNH1](https://www.tnh1.com.br/noticia/nid/com-csa-garantido-cbf-divulga-calendario-da-serie-d-com-96-clubes-a-partir-de-2026-veja-novo-formato/), [Itatiaia](https://www.itatiaia.com.br/esportes/futebol/futebol-nacional/brasileirao-serie-d/serie-d-passara-por-reformulacao-e-tera-96-clubes-a-partir-de-2026/)) |
| Série C | 20 clubes, 25 jogos, 4 sobem, 4 caem | **Corrigido** | 2026: 20 clubes, 19 rodadas em turno único + 2 quadrangulares (ida e volta) + final; 4 sobem, **2 caem**; 24 clubes em 2027, 28 em 2028 ([Band](https://www.band.com.br/nacional/brasil/ponte-preta-e-guarani-entenda-os-regulamentos-das-series-b-e-c-de-2026), [O Povo](https://www.opovo.com.br/esportes/futebol/2025/10/01/serie-c-e-d-mudanca-formato.html)) |
| Séries A e B | 20 clubes, 38 rodadas, 4 sobem/caem | Plausível | |
| Cota Série D | — | Confirmado | 2026: R$ 500 mil por clube na fase de grupos, até ~R$ 1,8 mi ao campeão; total R$ 65,5 mi ([TNH1](https://www.tnh1.com.br/noticia/nid/serie-d-confira-os-grupos-formato-datas-e-premiacao/), [Folha Vitória](https://www.folhavitoria.com.br/esportes/serie-d-do-brasileiro-vai-pagar-r-655-milhoes-em-premiacao-veja-detalhes/)) |
| Orçamento Série D | R$ 0,5–3 mi | Plausível | a cota da CBF sozinha é R$ 0,5–1,8 mi |
| Receita Série A | R$ 150 mi–1 bi | Plausível | Série A 2024: R$ 10,2 bi somados (Convocados, via [InfoMoney](https://www.infomoney.com.br/esportes/receita-de-clubes-da-serie-a-soma-r-102-bilhoes-em-2024-recorde-historico/amp/)); Flamengo 2023: R$ 1,4 bi (Sports Value) — o topo passa de R$ 1 bi |
| TV Série A | R$ 50–200 mi por clube | Plausível | TV ~35% das receitas da Série A |
| Série B/C/D: TV, público | ver especificação | Não verificado | |
| Folha salarial | 50–60% do orçamento | Plausível | |
| Contratos | A/B 2–5 anos; C/D 0,5–1 ano | Plausível | Lei Pelé: contrato de 3 meses a 5 anos (lembrança, não conferido) |
| Multa nacional | até 2000× o salário médio | Plausível | Lei Pelé, cláusula indenizatória (lembrança, não conferido) |
| Evolução | cresce 16–21, pico 25–29 (GOL/ZAG 28–32), declínio 31+ | Plausível | |
| OVR por divisão | A 73–85 (estrelas 90), B 64–72, C 56–63, D 45–55 | Igual ao GAME_DESIGN §12 | |

## Referências visuais (vídeo do usuário)

Câmera broadcast com amortecimento (já existe, X-59); vários ângulos de câmera; placar no canto superior esquerdo; pop-ups dinâmicos (jogador em destaque, posse, distância percorrida, nota ao vivo, melhores passadores); indicador sobre a cabeça do jogador controlado; avisos de ação no canto inferior esquerdo; gramado listrado realista; sombras dinâmicas; modelos 3D completos com uniformes; animação por Animator/Blend Trees. Situação de cada item: `DECISIONS.md` (X-67 e sugestões).
