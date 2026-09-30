# DECISIONS.md

Registro de decisões. Nenhuma decisão pendente deve ser tomada pelo Claude Code: ao encontrar uma, registre aqui e pergunte.

Formato de nova entrada: Id, título, contexto, opções, decisão (ou PENDENTE), data, fonte.

## Decidido

| Id | Decisão | Fonte |
| --- | --- | --- |
| X-01 | Jogo focado exclusivamente em modo carreira, single-player, 100% offline | GDD |
| X-02 | Clubes, jogadores, escudos e competições fictícios; sem marcas reais; UF real permitida | GDD |
| X-03 | Engine Unity 6 LTS, C#, IL2CPP ARM64 | Espec. técnica |
| X-04 | Android primeiro; iOS pós-MVP | Espec. técnica |
| X-05 | URP, Input System (toque), UI Toolkit para menus | Espec. técnica |
| X-06 | Núcleo em C# puro (zona pura) sem dependência da Unity; asmdef `noEngineReferences` + solution .NET paralela para `dotnet test` | Espec. técnica |
| X-07 | `MatchSetup` → `MatchResult` único para MatchEngine e QuickSim | Espec. técnica |
| X-08 | QuickSim resolve o "Simular" do jogador e todas as partidas da IA; MatchEngine headless usado para testes/calibração | Espec. técnica |
| X-09 | QuickSim e MatchEngine compartilham `Balance`, força por setor, regras de falta/cartão/lesão, fadiga, IA de substituição, fórmula de notas; calibração automatizada | Espec. técnica |
| X-10 | Balanceamento em JSON versionado; `Balance.Eval(Effect, jogador)`; curvas lineares por partes; ScriptableObjects não são fonte da verdade | Espec. técnica |
| X-11 | 18 atributos (Partida prevalece sobre os 14 do GDD) | Partida §11 |
| X-12 | Fadiga normalizada pela duração configurada (taxas para 6 min × 6/duração) | Espec. técnica §1 |
| X-13 | Determinismo por semente no mesmo aparelho/build; entre aparelhos não é requisito | Espec. técnica |
| X-14 | RNG com fluxos independentes por sistema | Espec. técnica |
| X-15 | Física própria da bola, sem PhysX/Rigidbody; passo fixo 50 Hz (valor inicial) | Espec. técnica / Partida |
| X-16 | IA em 3 camadas (time 5 Hz, função 10 Hz, individual 10 Hz escalonado) | Espec. técnica |
| X-17 | Save JSON + gzip, cabeçalho separado, `schemaVersion`, migrações, checksum, escrita atômica, 3 backups, autosave definido | Espec. técnica |
| X-18 | Cenas: Boot, Shell, Match + sandboxes de desenvolvimento | Espec. técnica |
| X-19 | Referências por Id inteiro no estado persistido | Espec. técnica |
| X-20 | Câmera broadcast lateral elevada dinâmica + radar | Partida §2 |
| X-21 | Controles: analógico flutuante + 4 botões contextuais + sprint dedicado, buffer e memória de sprint | Partida §3 |
| X-22 | Menus em retrato, partida em paisagem | GDD / Partida |
| X-23 | Modos: Jogar e Simular no MVP; Assistir pós-MVP; Simular não penaliza recompensas | GDD / Partida |
| X-24 | Escopo do MVP conforme `MVP_SCOPE.md` | GDD §11 / Partida §16 |
| X-25 | Roadmap: Stage 0 + Track A + Track B + Convergence | Espec. técnica §20 |
| X-26 | Vertical slice conforme `TECHNICAL_SPEC.md` §19 | Espec. técnica |
| X-27 | Sem pay-to-win | GDD |
| X-28 | Competições do MVP: 4 divisões × 16 clubes em pontos corridos + Copa 32 clubes jogo único | GDD §11 |
| X-29 | **D-06:** controle de versão com Git + Git LFS. Git LFS para binários/pesados (modelos, texturas, áudio e similares que surgirem). Serviço de hospedagem não decidido e não necessário agora | Decisão do usuário |
| X-30 | **D-09:** `Data/` na raiz do repositório, fora de `Assets/`; única fonte da verdade dos dados; consumida pela Unity e pela solution .NET/headless. `StreamingAssets` não é fonte da verdade; cópia para a build, se necessária, é gerada por etapa de build/empacotamento, sem duplicar a fonte | Decisão do usuário |
| X-31 | **D-19:** `MatchSetup` e `MatchResult` (e os tipos que carregam, como `MatchEvent`) são contratos do `Core`. MatchEngine (Jogar, Assistir, Headless) e QuickSim (Simular) usam o mesmo contrato sem depender um do outro | Decisão do usuário |
| X-32 | **D-11:** serializador JSON = **Newtonsoft JSON** (Json.NET, licença MIT, gratuito). Justificativa: a Unity mantém o pacote oficial `com.unity.nuget.newtonsoft-json`, compatível com Unity 6 e IL2CPP; `System.Text.Json` não faz parte do perfil .NET Standard 2.1 da Unity e exigiria DLLs manuais. Uma única abordagem para dados e save. Implementação: Unity usa `com.unity.nuget.newtonsoft-json` 3.2.1 (Json.NET 13.0.2) via `Unity/Packages/manifest.json`; a solution .NET usa o NuGet `Newtonsoft.Json` 13.0.2 (mesma versão). Só a assembly `Data` referencia (asmdef `precompiledReferences`); `Core` continua sem dependências. O loader usa LINQ to JSON (`JToken`), sem desserialização por reflexão, o que evita problemas de code stripping no IL2CPP. Save (B8) deve usar a mesma biblioteca | Decisão do usuário (29/09/2026) |
| X-33 | **D-20:** catálogo de `Effect` = **baseline v1** em `Data/Balance/effects.json`, com 49 efeitos derivados da tabela atributo → efeito de `TECHNICAL_SPEC.md` §8 e os 18 atributos cobertos. Linhas com contexto viram variantes explícitas do mesmo efeito: "ShotAngleError (área/fora)" → `ShotAngleErrorInBox`/`ShotAngleErrorOutOfBox`; "TurnSpeedLoss com bola" → `TurnSpeedLossWithBall`. `AerialDuel` usa Cabeceio 0,6 + Força 0,4; `FirstTouchError` usa Drible 0,7 + Compostura 0,3 (domínio usa Drible e Compostura, GAME_DESIGN §19); `TurnSpeedLossWithBall` usa Drible 0,6 + Agilidade 0,4. Curvas não lineares (pontos em 1, 30, 50, 70, 80, 90, 99; 50 = médio, 70–80 acima da média, 90–99 elite), seguindo a regra de percepção; valores documentados usados como âncora quando existem (velocidade 7,0–9,5 m/s, aceleração 1,3–0,7 s, parada 0,5–0,3 s, recuperação de curva 0,4–0,2 s, reação GK 0,30–0,15 s, Resistência reduz gasto até 40%, penalidade de energia baixa ~12%, Passe 90 erra ~¼ do Passe 40, pressão +30–80%). Valores são baseline v1, sujeitos a calibração durante o desenvolvimento e os testes da partida; não representam o balanceamento final. Recalibrar = editar o JSON, sem alterar a lógica | Decisão do usuário (30/09/2026) |

## Pendente

| Id | O que decidir | Por que importa | Bloqueia? | Precisa estar resolvida em |
| --- | --- | --- | --- | --- |
| D-01 | Primeira trilha: Track A (Partida) ou Track B (Carreira) | Define o que se valida primeiro (risco técnico da partida vs valor da gestão, recomendação do GDD) | Sim, após Stage 0 | Fim da Stage 0 |
| D-02 | Origem dos modelos 3D e animações (Asset Store, bibliotecas gratuitas com licença comercial verificada, artista contratado) | Define pipeline de animação, esqueleto e orçamento de performance | Não para placeholders; sim para conteúdo final | Não bloqueia o VS (placeholders aceitos); obrigatória antes de C4 |
| D-03 | Aparelhos Android de referência (um de entrada, um intermediário) | Base de todos os critérios de performance | Sim para aceite de performance | Antes do aceite de A7 (e de qualquer teste de performance) |
| D-04 | Idiomas no lançamento (só PT-BR ou PT-BR + outros) | Estrutura de strings e layout da UI | Parcial: tabelas de strings já são regra; escopo de tradução não | Antes de C3 |
| D-05 | Mecânica do tiro de meta (proposta: Passe = saída curta, Alto = longa; IA decide por mentalidade e pressão) | Não definido no design da partida | Sim para o VS (inclui tiro de meta simples) | Antes de A7 |
| D-07 | Monetização | Pode exigir SDK e telas | Não | Antes de C5 |
| D-08 | Versão mínima de Android / nível de API | Regras da Unity 6 e da Play Store mudam | Não até o build de loja | Antes de C5 (conferir regras vigentes) |
| D-10 | Câmera: Cinemachine ou câmera própria | Implementação da câmera broadcast | Sim para a câmera de jogo | Antes de A2 |
| D-12 | Tamanho do mundo no MVP (64 clubes das 4 divisões ou ~150 do design-alvo, clubes fora da pirâmide para a Copa) | Gerador de mundo, performance da carreira, Copa | Sim | Antes de B1 |
| D-13 | Quantos sobem/caem entre divisões de 16 clubes | Regras de competição | Sim | Antes de B3 |
| D-14 | Critério de classificação para a Copa Nacional de 32 clubes | Formato da Copa | Sim | Antes de B3 |
| D-15 | Traits no MVP: quais, quantos, efeito só na carreira ou também em campo (GDD fala em "no máximo 4 no MVP"; Partida deixa traits em campo para depois) | Dados do jogador, mercado, partida | Sim para gerador de jogadores com traits | Antes de B1 (ou decidir que B1 gera sem traits) |
| D-16 | Nome do jogo (nome de trabalho ACESSO; checar INPI e lojas) | Marca, loja | Não | Antes de C5 |
| D-17 | Dificuldade no MVP: dificuldade da carreira (orçamento/paciência) e da partida (reação/decisão da IA, 4 níveis citados no GDD original) entram ou não | Parâmetros da diretoria e da IA | Parcial | Antes de B7 (carreira) e A9 (partida) |
| D-18 | Criar clube próprio (nome, cores, escudo por template) no MVP ou só escolher entre clubes da D | Fluxo de nova carreira | Não para o núcleo | Antes de C3 |

## Sugestões registradas (não aprovadas)

Nenhuma até o momento. Novas ideias entram aqui antes de qualquer implementação (ver `MVP_SCOPE.md`).
