# CONSISTENCY_REVIEW.md

Revisão cruzada do pacote documental (25/09/2026). Não é instrução para o Claude Code; é o registro de verificação.

## Verificações

| # | Verificação | Resultado |
| --- | --- | --- |
| 1 | Sem contradições entre documentos | OK após correções: carrinho estava em A8 no rascunho do roadmap e foi devolvido para A9 (como na especificação); local de `MatchSetup`/`MatchResult` divergia entre ARCHITECTURE e a especificação → registrado como D-19; "sala de troféus" aparecia na tela Club da especificação, mas está fora do MVP pelo GDD → marcada como fora do MVP no TECHNICAL_SPEC |
| 2 | MVP não ampliado | OK. MVP_SCOPE reproduz GDD §11 + Partida §16. Staff (3 funções) vem do GDD §2 ("No MVP, apenas 3 funções"). Nada novo |
| 3 | Nenhuma pendência decidida em silêncio | OK. 19 pendências em DECISIONS (6 da especificação + 13 encontradas na consolidação: monetização, Android mínimo, pasta Data, Cinemachine, serializador, tamanho do mundo, sobe/desce com 16 clubes, classificação da Copa, traits no MVP, nome, dificuldade, criar clube, local de MatchSetup) |
| 4 | Arquitetura compatível com a especificação | OK. ARCHITECTURE e TECHNICAL_SPEC derivam da mesma fonte; TECHNICAL_SPEC é a especificação com pendências marcadas |
| 5 | Design da partida compatível com "Partida — design aprofundado" | OK. GAME_DESIGN Parte III usa só o documento da Partida; única adição é a normalização da fadiga, que é decisão da especificação técnica (X-12) |
| 6 | GDD não sobrescreve decisões posteriores | OK. 14 → 18 atributos, câmera top-down → broadcast, controles antigos → layout da Partida, "Simular rende menos XP" → removido |
| 7 | Roadmap não antecipa funcionalidades | OK. Cada etapa tem lista "Não implementar"; itens pós-MVP aparecem só como exclusões |
| 8 | QuickSim e MatchEngine compartilham regras | OK. ARCHITECTURE §13, TECHNICAL_SPEC §10, X-09, teste de calibração em C2 e TEST_PLAN |
| 9 | Separação Core/Unity preservada | OK. CLAUDE §5–6, ARCHITECTURE §2–3, REPOSITORY_STRUCTURE, Stage 0 com solution .NET |
| 10 | Testes e performance refletidos no roadmap | OK. Testes por etapa; zero alocação desde A1/A2; marcos de performance em A7, C1, C3, C5 |
| 11 | Nenhuma tecnologia contraditória | OK. Só a stack da especificação; Cinemachine e serializador ficaram PENDENTES como na especificação |
| 12 | Nenhuma funcionalidade nova por sugestão minha | OK. Estruturas organizacionais adicionadas (pasta `dotnet/`, `Data/TestRanges/`, `CONSISTENCY_REVIEW.md`) são organização de repositório/documentação, sem funcionalidade de jogo; nomes exatos podem ser trocados na Stage 0 |

## Checklist final

### DEFINIDO (pode começar)
- D-06 Git + Git LFS; D-09 `Data/` na raiz como fonte da verdade única; D-19 contratos `MatchSetup`/`MatchResult` no `Core`.
- Visão, identidade básica, escopo do MVP e fora do MVP.
- Stack: Unity 6 LTS, C#, Android, URP, Input System, UI Toolkit, IL2CPP.
- Arquitetura zona pura × Unity, assemblies, dependências, tipos centrais, Ids, RNG com fluxos, eventos, serviços.
- MatchEngine (sistemas, ordem do passo), física da bola, IA em 3 camadas, controles, câmera, atributos e efeitos, táticas do MVP.
- QuickSim, compartilhamento de regras e calibração.
- Carreira por datas, sistemas, mercado, economia com livro-caixa.
- Save (formato, versionamento, migração, validação, backup, autosave).
- Cenas e telas.
- Plano de testes, invariantes, definição de pronto.
- Roadmap com Stage 0, A1–A9, B1–B8, C1–C5 e vertical slice.

### PENDENTE, MAS NÃO BLOQUEANTE (agora)
- D-02 Origem da arte/animações (placeholders aceitos até C4).
- D-04 Idiomas (até C3).
- D-07 Monetização (até C5).
- D-08 Android mínimo (até C5).
- D-16 Nome do jogo (até C5).
- D-18 Criar clube próprio (até C3).
- D-10 Cinemachine ou câmera própria (até A2).
- D-11 Serializador (até B8, salvo se o loader da Stage 0 precisar).

### PENDENTE E POTENCIALMENTE BLOQUEANTE
- D-01 Primeira trilha (A ou B) — fim da Stage 0.
- D-03 Aparelhos de referência — aceite de A7 e testes de performance.
- D-05 Tiro de meta — antes de A7.
- D-12 Tamanho do mundo — antes de B1.
- D-15 Traits no MVP — antes de B1 (ou decidir gerar sem traits).
- D-13 Sobe/desce com 16 clubes — antes de B3.
- D-14 Classificação para a Copa — antes de B3.
- D-17 Dificuldade no MVP — antes de A9 (partida) e B7 (carreira).

## Revisão 2 — incorporação de D-06, D-09 e D-19

| Verificação | Resultado |
| --- | --- |
| D-06 em DECISIONS, CLAUDE, TECHNICAL_SPEC, REPOSITORY_STRUCTURE, ROADMAP | OK; nenhuma menção restante a "Unity Version Control" como opção |
| D-09: `Data/` na raiz, fonte única, lida por Unity e .NET; `StreamingAssets` não é fonte; cópia só por empacotamento | OK em todos os documentos; nenhuma menção restante a "PENDENTE" para a pasta Data |
| D-19: contratos no `Core`; `Match` e `Simulation` independentes | OK; removidas as menções a contratos em `Simulation` (TECHNICAL_SPEC §16, REPOSITORY_STRUCTURE, ARCHITECTURE) |
| Outras pendências | Inalteradas (D-01 a D-05, D-07, D-08, D-10 a D-18) |
| MVP | Inalterado |
| Observação não resolvida | O diagrama conceitual da decisão D-09 mostra `Tests/` e `Tools/` na raiz; a estrutura documentada mantém `Tests/` e `Tools/` dentro de `Assets/_Game/` (Especificação §16). D-09 decide só o local de `Data/`; se a intenção for mover `Tests/`/`Tools/` ou colocar o projeto Unity na raiz do repositório, isso é uma nova decisão |
