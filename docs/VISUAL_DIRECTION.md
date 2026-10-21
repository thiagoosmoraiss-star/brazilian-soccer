# Direção visual — referência de transmissão realista (estilo PES 2015)

> **Situação:** proposta registrada em X-123 (20/10/2026), **só documentação; não autoriza implementação**. A arte 3D continua na F7b (pré-requisito D-02) e os níveis gráficos na F7c (pré-requisito D-27). Nenhuma fase nova foi criada.
>
> **Propriedade intelectual:** o PES 2015 é só uma referência estética geral (enquadramento de transmissão, iluminação, materiais, apresentação). Nenhum modelo, rosto, uniforme, escudo, marca, textura, interface ou outro conteúdo da Konami será copiado ou incorporado. Todo conteúdo será original ou licenciado, com a licença registrada em `REFERENCES.md` (critério da F7b).

## 1. Regra de qualidade

Preservar a fidelidade visual sempre que for tecnicamente viável e adaptar só o que realmente precisar. Para cada recurso:

1. definir a solução de maior fidelidade;
2. estimar o custo de GPU, CPU e memória;
3. otimizar antes de reduzir;
4. se ainda for necessário, adaptar só o recurso problemático;
5. descrever a diferença visual entre a solução ideal e a adaptada;
6. comprovar o resultado com captura visual da cena completa e com medição no aparelho.

Uma adaptação só é aceita com medição real que mostre a necessidade (seção 6). Os custos abaixo são **estimativas de projeto**, não medições.

## 2. Estado atual do projeto (auditoria de 20/10/2026, só leitura)

| Área | Encontrado |
| --- | --- |
| Unity / pacotes | Unity 6000.3.1f1; URP 17.3.0 (Shader Graph 17.3.0 incluído por dependência); Cinemachine 3.1.7; Input System 1.17.0. Sem TextMeshPro, Adaptive Performance nem Addressables |
| Pipeline | URP, renderer **Forward** (`Default_Forward_Renderer`), sem renderer features; SRP Batcher ligado; HDR ligado; render scale 1; 6 níveis de qualidade (Very Low a Ultra), **Android usa Medium** |
| URP Medium (Android) | MSAA desligado (1×); sombra da luz principal 1024, distância 20 m, 1 cascata, sem sombra suave; sem depth/opaque texture; color grading LDR |
| Espaço de cor | **Gamma** (`m_ActiveColorSpace: 0`) |
| Pós-processamento | Só o `DefaultVolumeProfile` do URP, tudo neutro; nenhuma Volume nas cenas e as câmeras sem dados de câmera URP: **pós-processamento efetivamente desligado** |
| Cenas | Boot, Shell e Match, cada uma com Main Camera (FOV 60, skybox padrão) e uma Directional Light em tempo real; sem lightmaps, light probes, reflection probes nem LightingSettings |
| Arte | **Nenhum** shader, material, modelo, textura, animação, prefab ou fonte em `Assets/` |
| Gramado | Um `Plane` verde liso; linhas como cubos achatados; **sem listras, círculo central, pequena área nem redes** |
| Jogadores | Primitivas (cápsula, esfera, cubos) com animação procedural por código (`match_view.json`); sem Animator nem esqueleto |
| Bola | Esfera; sombra falsa (disco preto) |
| Materiais | Criados em tempo de execução, um por cor, a partir de `Universal Render Pipeline/Lit` (`RuntimeMaterialBuildStep`, X-62) |
| Câmera | Câmera de transmissão própria sobre Cinemachine 3 (`BroadcastCamera`, `match_view.json`: inclinação 40°, distância 27–40 m, FOV 38, zoom perto do gol, lookahead) |
| HUD e menus | UI Toolkit montado em código (placar, relógio, faixa, radar, FPS, cartões, pausa, substituições, táticas); controles na tela em IMGUI; fonte padrão |
| Android | IL2CPP, ARM64, minSdk 25, APIs gráficas automáticas, Swappy ligado, renderização multithread, sem resolução dinâmica; compressão de textura sem ASTC explícito; `targetFrameRate = 60` na partida |
| Medições | 14/10, só primitivas: S25 Ultra 60 fps; S10+ e A07 entre 50 e 60 fps. A medição da F0 no A07/S10+ continua pendente. Sem testes de desempenho automatizados; FPS médio e pior quadro no HUD |

## 3. Auditoria por elemento (A a I)

Classificação: **[I]** já implementado · **[V]** viável com a infraestrutura atual · **[VA]** viável, depende de assets ou infraestrutura · **[AD]** viável com adaptação (só se a medição exigir) · **[NR]** não recomendado como padrão mobile (só no perfil avançado).

### A. Gramado

| Recurso | Hoje | Classe | Máxima fidelidade | Custo esperado | Otimização antes de reduzir | Alternativa e diferença visual |
| --- | --- | --- | --- | --- | --- | --- |
| Listras de corte | Não | [V] | Listras no shader (bandas por coordenada do campo, variação de tom e de reflexo conforme o ângulo da câmera, como o gramado real) | Baixo (matemática no shader de uma superfície) | Calcular as bandas no shader, sem textura extra | Nenhuma redução prevista |
| Variação de tom e desgaste | Não | [VA] | Texturas de detalhe (albedo, normal, rugosidade) em alta resolução, máscara de desgaste nas áreas e no meio-campo | Médio (amostras de textura numa área grande da tela) | ASTC, mipmaps, uma máscara de baixa resolução para o desgaste, tiling com variação para evitar repetição | Básico: menos camadas de detalhe; o campo continua com listras e desgaste, com menos microdetalhe de perto |
| Normal map e microtextura | Não | [VA] | Normal de detalhe com tiling fino e desvanecimento por distância | Médio | Desligar a microtextura além de uma distância; mipmaps | Básico: sem microtextura distante (pouco visível na câmera de transmissão) |
| Linhas integradas | Cubos achatados | [V] | Linhas pintadas no próprio shader do gramado (com leve desgaste e o mesmo tratamento de luz), incluindo círculo central e pequena área | Baixo | Desenhar por distância a segmentos no shader; elimina objetos extras | Nenhuma redução |
| Clima e umidade | Não | [VA] | Parâmetros de umidade (mais reflexo, cor mais escura) ligados ao clima | Baixo a médio | Só parâmetros do mesmo shader | Básico: reflexo molhado simplificado |
| Marcas de contato e carrinho | Não | [AD] | Decalques persistentes de marcas na grama | Médio (decalques, memória) | Poucos decalques reciclados, atlas | Básico: sem marcas persistentes |

### B. Jogadores

| Recurso | Hoje | Classe | Máxima fidelidade | Custo esperado | Otimização | Alternativa |
| --- | --- | --- | --- | --- | --- | --- |
| Modelo humanoide e biotipos | Primitivas | [VA] (D-02) | Um esqueleto humanoide para todos, malhas com biotipos (altura, massa) por blend shapes ou escala de ossos | Alto (22 personagens com skinning) | Esqueleto único, GPU skinning, LOD 2–3 níveis, Animator com culling, SRP Batcher (TECHNICAL_SPEC §18) | Básico: LOD mais agressivo à distância; o rosto detalhado só aparece de perto |
| Rosto, cabelo, barba, olhos | Não | [VA] | Rostos com textura de alta resolução, cabelo em cartões com alpha | Alto (memória; transparência do cabelo) | Atlas de rostos, cabelo com alpha test em vez de transparência, rosto detalhado só no LOD 0 (replays e apresentação) | Básico: cabelo e rosto simplificados na câmera de jogo, iguais aos do avançado nos replays |
| Uniformes (tecido, número, patrocínio fictício) | Cor sólida | [VA] | Material de tecido com normal de trama, números e nomes compostos no shader, patrocínio fictício | Médio | Composição no shader em vez de uma textura por camisa; atlas | Nenhuma redução de legibilidade; menos detalhe de trama no básico |
| LODs e compressão | Não | [VA] | 3 níveis de LOD, ASTC | Reduz custo | — | — |
| Animações, transições, IK, goleiros | Procedural | [VA] (D-02) | Animações capturadas com blend trees, IK de pés e de mão do goleiro, transições suaves, sempre seguindo o estado da simulação | Médio a alto (CPU do Animator para 22) | Culling, Animator simples, IK só nos jogadores próximos da bola; plano B: animação baked em textura (IMPLEMENTATION_PLAN §5) | Básico: IK só no portador e no goleiro |

### C. Bola

| Recurso | Hoje | Classe | Máxima fidelidade | Custo | Otimização | Alternativa |
| --- | --- | --- | --- | --- | --- | --- |
| Material, painéis, costuras | Esfera lisa | [VA] | Modelo com painéis, normal das costuras, brilho | Baixo | Uma malha leve | Nenhuma |
| Rotação e efeito | Não visível | [V] | Rotação a partir do giro da simulação (`Ball.Spin`) | Baixo | — | Nenhuma |
| Sombra | Disco falso | [V] | Sombra real da luz principal mais uma sombra de contato suave | Baixo | Manter a sombra de contato falsa como complemento | Básico: só a sombra de contato |
| Partículas de chute e rastro | Não | [AD] | Pequenas partículas de grama no chute e rastro sutil no chute forte | Baixo a médio (overdraw) | Poucas partículas, atlas | Básico: desligadas |

### D. Câmera

| Recurso | Hoje | Classe | Máxima fidelidade | Custo | Otimização | Alternativa |
| --- | --- | --- | --- | --- | --- | --- |
| Câmera lateral de transmissão | **[I]** (`BroadcastCamera`) | [I] | Ajustar enquadramento, FOV e altura comparando com transmissões reais | Baixo | — | — |
| Zoom contextual e seguimento suave | **[I]** (zoom perto do gol, lookahead, suavização) | [I] | Refinar por situação de jogo (bola parada, contra-ataque) | Baixo | — | — |
| Câmeras alternativas | Não | [V] | Presets (GAME_DESIGN §16; no MVP só Padrão) | Baixo | — | — |
| Replays e cenas de apresentação | Não | [VA] | Replay do gol a partir do estado da simulação gravado; entrada em campo, comemoração | Médio (memória do buffer de replay) | Gravar só estados compactos, já disponíveis pelo snapshot | Básico: replay mais curto |

### E. Iluminação

| Recurso | Hoje | Classe | Máxima fidelidade | Custo | Otimização | Alternativa |
| --- | --- | --- | --- | --- | --- | --- |
| Espaço de cor | Gamma | [V] | **Linear**, condição para materiais e luz realistas | Baixo (exige GLES3/Vulkan, compatível com minSdk 25) | — | Nenhuma: recomendado para todos os perfis, a confirmar com medição |
| Dia, entardecer, noite, nublado | Uma luz fixa | [VA] | Presets de luz e céu por horário e clima, em dados | Baixo a médio | Uma luz direcional, céu e ambiente por preset | Nenhuma redução de presets |
| Refletores e noite | Não | [AD] | Torres de luz com halo, sombras múltiplas suaves dos jogadores | Alto (várias sombras em tempo real) | Uma sombra real principal e sombras múltiplas falsas (decalques) por jogador | Básico e intermediário: sombras múltiplas falsas, diferença pequena na câmera de transmissão |
| Sombras coerentes | Hard, 1024, 20 m | [V] | Sombra suave com cascatas cobrindo a área visível, resolução alta | Médio a alto | Distância de sombra ajustada ao enquadramento, só jogadores, bola e estádio próximo projetam | Básico: menos resolução e uma cascata; mantém a sombra do jogador |
| Luz ambiente e rim light | Ambiente do skybox padrão | [V] | Ambiente por preset, light probes para os jogadores, rim light sutil no shader | Baixo | Probes baked | Nenhuma |
| Iluminação baked | Não | [VA] | Lightmaps no estádio (estático), luz em tempo real só para o que se move | Médio (memória) | Lightmaps comprimidos | Básico: lightmaps de resolução menor |
| Reflexos | Não | [VA] | Reflection probes baked por preset; reflexo do gramado molhado | Baixo a médio | Probes baked, sem reflexo em tempo real | Reflexo em tempo real: [NR] |

### F. Estádio e torcida

| Recurso | Hoje | Classe | Máxima fidelidade | Custo | Otimização | Alternativa |
| --- | --- | --- | --- | --- | --- | --- |
| Arquitetura, arquibancadas | Não (só um apron) | [VA] | Estádio modelado com materiais realistas, iluminação baked | Médio | LOD, batching estático, atlas, lightmaps | Básico: LOD mais agressivo |
| Torcida animada | Não | [VA] | Torcida em cartões (impostores) com animação por atlas e variação de cor, densa | Médio a alto (overdraw, contagem) | GPU instancing, alpha test, atlas, animação no shader | Básico: menos densidade e menos quadros de animação; de longe a diferença é pequena |
| Redes, bancos, placas | Não | [VA] | Rede com malha e física visual simples no gol, bancos, placas de publicidade fictícia | Baixo a médio | Rede animada só no gol; placas em atlas | Básico: rede estática |

### G. Clima e efeitos

| Recurso | Classe | Máxima fidelidade | Custo | Alternativa |
| --- | --- | --- | --- | --- |
| Chuva, gramado molhado, respingos | [AD] | Partículas de chuva, gramado molhado, respingos nos passos | Médio a alto (overdraw) | Básico: chuva mais leve e sem respingos; o gramado molhado continua |
| Poeira, confetes, comemorações | [AD] | Partículas e efeitos de comemoração | Médio | Básico: menos partículas |

Todos são opcionais e escaláveis por perfil (seção 5).

### H. Pós-processamento

| Recurso | Hoje | Classe | Recomendação |
| --- | --- | --- | --- |
| Color grading e tonemapping | Desligado | [V] | Tonemapping e LUT de transmissão em todos os perfis (custo baixo); requer ligar o pós-processamento na câmera e uma Volume |
| Bloom discreto | Desligado | [V] / [AD] | Avançado e intermediário: bloom leve; básico: só se a medição permitir |
| Anti-aliasing | MSAA desligado no Medium | [V] | MSAA 2× ou 4× (linhas do campo e contornos dependem dele); comparar com FXAA/SMAA e TAA por medição |
| Vinheta | Desligada | [V] | Sutil, custo baixo |
| DoF | Desligado | [NR] | Só em replays e apresentação, nunca na câmera de jogo |
| SSAO | Desligado | [NR] | Só avançado, se a medição mostrar ganho visível; no estádio, AO baked |
| Motion blur | Desligado | [NR] | Não na câmera de jogo (prejudica a leitura) |

### I. Interface

| Recurso | Hoje | Classe | Máxima fidelidade | Observação |
| --- | --- | --- | --- | --- |
| HUD de transmissão (placar, tempo, acréscimos, gols, cartões, substituições, estatísticas) | **[I] parcial** (placar, relógio, faixa, cartões, radar; UI Toolkit) | [V] | Grafismo de transmissão próprio, animações de entrada, acréscimos e estatísticas no intervalo | O tema vem de dados na F7a |
| Menus próprios e tipografia | Fonte padrão | [VA] | Fonte licenciada legível (D-29/F7a) | Fontes vão para Git LFS |
| Layout adaptável | **[I]** (escala por tela, área segura na Shell) | [I] | Conferir proporções 19,5:9 a 21:9 | — |
| Controles na tela | IMGUI | [V] | Migrar para UI Toolkit com o tema (F6/F7a) | — |

## 4. Otimizações candidatas (não autorizadas; confirmar compatibilidade antes)

| Técnica | Situação | Observação |
| --- | --- | --- |
| SRP Batcher | Ligado | Manter materiais compatíveis |
| GPU instancing | Não usado | Torcida, placas, decalques |
| Atlas e materiais compartilhados | Não aplicável hoje | Uniformes compostos no shader, torcida e placas em atlas |
| LOD e mipmaps | Não aplicável hoje | 2–3 LODs nos jogadores e no estádio |
| ASTC | Não configurado explicitamente | Definir no build Android quando houver texturas |
| Baked lighting e light probes | Nada baked | Estádio estático em lightmaps; probes para jogadores |
| Draw calls e overdraw | Primitivas, um material por cor | Medir com Frame Debugger e Profiler no aparelho |
| Sombras seletivas | Todos os renderers projetam | Só jogadores, bola e partes próximas do estádio |
| Transparências | Nenhuma | Preferir alpha test em cabelo, rede e torcida |
| Forward+ | Forward | Avaliar se refletores e efeitos noturnos precisarem de muitas luzes |
| Resolução dinâmica / render scale | Desligado / 1 | Recurso de estabilidade por perfil, antes de cortar efeitos |
| Adaptive Performance | Pacote ausente | Avaliar para controle térmico no Samsung (exige decisão de dependência, CLAUDE.md §3) |

## 5. Perfis gráficos propostos (para a D-27; mesma identidade visual em todos)

| Perfil | Aparelho de referência | Meta | Mantém | Ajusta |
| --- | --- | --- | --- | --- |
| Básico | Galaxy A07 | ≥ 30 fps estáveis | Linear, listras e linhas no shader, uniformes legíveis, sombra real dos jogadores, color grading, torcida | Resolução de sombra, cascatas, densidade de torcida, LOD, partículas, MSAA (2× ou alternativa), render scale |
| Intermediário | Galaxy S10+ | Próximo de 60 fps | Tudo do básico, mais microtextura, bloom leve, MSAA, rede animada | Sombras múltiplas falsas à noite, clima simplificado |
| Avançado | Galaxy S25 Ultra (não é critério de aceite, X-63) | 60 fps | Máxima fidelidade da seção 3 | SSAO e DoF de replay opcionais |

**Regras:** 30 fps estáveis é o mínimo inicial; 60 fps nos compatíveis. Nenhuma meta é declarada atingida sem medição. Ajustes dinâmicos candidatos (em ordem, antes de cortar efeitos): render scale, densidade de torcida, distância e resolução de sombra, partículas.

## 6. Critérios de desempenho e de adaptação

- **Aparelhos:** A07 (entrada) e S10+ (intermediário) são os de aceite (X-63); S25 Ultra para o perfil avançado. A medição da F0 no A07/S10+ continua pendente e é a base de comparação.
- **Cenas de maior carga:** 22 jogadores e a bola na mesma metade (escanteio e ataque na área), câmera virada para a arquibancada cheia, noite com refletores, chuva, replay de gol, troca de tempo.
- **Métricas:** fps médio e 1% pior; tempo de quadro de CPU e GPU (Profiler, Android GPU Inspector ou ferramentas do fabricante); memória total (meta < ~600 MB); picos de GC (zero no loop da partida); temperatura e queda de clock após 15–20 min contínuos; consumo de bateria por partida.
- **Quando adaptar:** só quando a medição na cena de maior carga ficar abaixo da meta do perfil e a otimização do mesmo recurso (seção 4) não resolver; a adaptação escolhida é a de menor diferença visual na cena completa.
- **Avaliação visual:** comparação da cena completa em movimento, na câmera de transmissão, gravada no aparelho, não só capturas isoladas de material.

## 7. Plano futuro por etapas (dentro da F7b e da F7c; nenhuma iniciada)

| Etapa | Conteúdo | Aceite visual | Aceite técnico |
| --- | --- | --- | --- |
| V0 — Decisões | D-02 (origem e licença de modelos e animações), D-27 (perfis), espaço de cor Linear, fonte (D-29), orçamento de conteúdo | Usuário aprova referências originais (moodboard sem conteúdo da Konami) | Licenças registradas em `REFERENCES.md` |
| V1 — Cena representativa | Gramado com listras e linhas no shader, 22 jogadores com uniformes (modelo provisório licenciado), bola, iluminação diurna, câmera de transmissão atual, estádio de fundo simples, HUD básico; perfis básico/intermediário/avançado | Usuário aprova a cena completa gravada no aparelho | Medição nas cenas de maior carga no A07 e no S10+; relatório de CPU, GPU e memória |
| V2 — Jogadores e animação | Modelos finais, rostos, animações e IK | Comparação com V1 | Metas mantidas |
| V3 — Estádio, torcida e noite | Estádio completo, torcida, refletores, reflexos baked | Idem | Idem |
| V4 — Clima, efeitos, pós-processamento | Chuva, partículas, color grading final, bloom | Idem | Idem |
| V5 — Apresentação | Replays, entrada em campo, HUD de transmissão completo | Idem | Idem |

V1 é a primeira etapa gráfica recomendada: serve para avaliar qualidade e desempenho antes de ampliar. Pela ordem do roadmap, a F7b vem depois da F6; antecipar a V0 ou a V1 exige autorização do usuário.
