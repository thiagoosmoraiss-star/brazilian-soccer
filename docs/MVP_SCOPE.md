# MVP_SCOPE.md

Fontes: GDD §11 (MVP da carreira) e "Partida — design aprofundado" §16 (MVP da partida). Onde as fontes divergem ou são omissas, o item está marcado **PENDENTE** e referenciado em `DECISIONS.md`.

**Definição do MVP:** uma carreira de 3 temporadas jogáveis, com 4 divisões em pontos corridos, partida jogável + simulação, mercado básico e acesso/rebaixamento.

**Critério de sucesso (teste com 5–10 pessoas):** a maioria joga até o fim da 1ª temporada; parte chega à 3ª; ninguém quebra a economia em 3 temporadas.

## Dentro do MVP

### Carreira
- 4 divisões de 16 clubes, pontos corridos (30 rodadas).
- Copa Nacional mata-mata, jogo único, 32 clubes.
- Acesso e rebaixamento. PENDENTE D-13: quantos sobem/caem com 16 clubes.
- Classificação para a Copa: PENDENTE D-14.
- Tamanho do mundo: PENDENTE D-12.
- Escolha de clube entre opções da Divisão D. Criar clube próprio: PENDENTE D-18.
- Objetivo da temporada + confiança da diretoria + demissão.
- Reputação de clube e treinador (esta sem propostas de outros clubes).
- Dificuldade da carreira: PENDENTE D-17.
- 1 save local.

### Jogadores
- 18 atributos, OVR por posição, potencial oculto (faixa), idade, posições.
- Evolução e queda pela curva de idade.
- Energia, moral simples, forma simples, lesões simples, suspensões.
- Base gerando 3 jovens por ano (fixo).
- Traits: PENDENTE D-15 (quantos/quais, e se têm efeito na carreira, na partida ou em ambas).

### Mercado
- Contratar, vender, agentes livres, renovar, pré-contrato, propostas da IA.
- Scouting simples (faixa de atributos).
- IA de mercado com reposição de elencos.

### Economia
- Cota de TV, bilheteria, 1 patrocinador, premiações, salários, manutenção, caixa, orçamento (teto e verba), regras de caixa negativo.

### Clube
- Instalações: Estádio e Centro de treinamento, com obras e requisito de estádio por divisão.
- Comissão técnica: preparador físico, auxiliar técnico, olheiro.

### Partida
- Modos Jogar e Simular (QuickSim). Duração 4/6/10 min.
- Câmera broadcast dinâmica + radar, preset único.
- Analógico flutuante, 4 botões + sprint, buffer de entrada, memória de sprint.
- Troca automática Semi com histerese e travas; troca manual por toque.
- Movimento com Velocidade e Agilidade, sprint e fadiga (normalizada pela duração).
- Passe curto, longo, enfiada, lançamento, cruzamento alto; assistência Semi.
- Chute com força e direção, cabeceio, pressão e pé ruim.
- Condução, corte, arrancada, proteção.
- Contenção, desarme em pé, carrinho, interceptação, 2º defensor.
- Goleiro: posicionamento, reação, encaixe/espalma, saída 1×1, cruzamentos simples, rebote.
- IA: fases do jogo, posição-alvo por formação, compactação, zona, cobertura, apoio e ruptura básicos.
- Física analítica completa.
- 18 atributos com efeito.
- Formações 4-4-2, 4-3-3, 4-2-3-1; mentalidade, linha, pressão; substituições na pausa.
- Lateral, escanteio, falta direta sem curva, pênalti; faltas, cartões, impedimento.
- Tiro de meta: PENDENTE D-05 (mecânica).
- Retomar partida após o app ir para segundo plano.
- Som básico (torcida, apito, chute).

### Apresentação e UI
- Todas as telas de `TECHNICAL_SPEC.md` §15 nas funcionalidades acima.
- Identidade básica (cores + fontes).

### Plataforma
- Android, offline.

## Fora do MVP

- Modo Assistir.
- Estaduais, grupos, quadrangulares, D com grupos regionais, Copa com ida/volta, competições continentais.
- Objetivos de longo prazo, sala de troféus, ídolos, linha do tempo do clube.
- Assumir outro clube após demissão; propostas ao treinador.
- Empréstimos, cláusulas de rescisão, parcelas, trocas, bônus por gol.
- Rede de olheiros e observação por tempo.
- Departamento médico, níveis de base, loja/marketing, demais funções de staff.
- Loja/produtos, preço de ingresso ajustável, remanejamento de verba, empréstimo bancário.
- Múltiplos saves, nuvem.
- Câmera: presets Alta/Próxima, replays.
- Controles: layout editável, modo canhoto, vibração, troca direcional, assistências Completa/Manual.
- Colocado, cruzamento rasteiro/recuo, enfiada aérea, passe de primeira refinado, assistências de chute configuráveis.
- Finta de corpo, dribles especiais.
- Disputa de corpo detalhada.
- Frango/erros especiais do goleiro, reposição tática.
- Marcação na área refinada, avisos do auxiliar.
- Traits em campo.
- Largura, laterais, presets táticos rápidos, formações 3-5-2, 5-3-2, 4-1-4-1, 4-4-1-1.
- Curva em falta, barreira editável, disputa de pênaltis visual.
- Torcida dinâmica, comemorações, animações e cenas especiais além do básico.
- iOS.
- Dados reais licenciados, rostos realistas.
- Qualquer funcionalidade online.

## Fora do escopo mesmo que pareça uma boa ideia

Ideias novas não entram no MVP só porque parecem úteis, rápidas ou "óbvias". Isso vale para o Claude Code e para qualquer sugestão feita durante o desenvolvimento.

Regra:

1. A ideia é registrada em `DECISIONS.md` como sugestão PENDENTE, com motivo.
2. Nada é implementado até que a sugestão seja aprovada e o `MVP_SCOPE.md` e o `ROADMAP.md` sejam atualizados.
3. Melhorias de qualidade dentro de uma funcionalidade do MVP (corrigir bug, ajustar balanceamento em dados) são permitidas; novas funcionalidades, telas, modos, sistemas ou tipos de dado não são.

Exemplos que ficam fora mesmo parecendo pequenos: mais uma formação, um trait extra, um tipo de drible, uma tela de estatísticas avançadas, uma notificação nova, conquistas, modo treino, editor de clube, compartilhamento, modo rápido fora da carreira.
