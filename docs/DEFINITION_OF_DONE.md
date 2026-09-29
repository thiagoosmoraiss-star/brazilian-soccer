# DEFINITION_OF_DONE.md

Compilar não é estar pronto. Uma funcionalidade só está concluída quando todos os itens aplicáveis abaixo estão cumpridos e verificados (executados, não presumidos).

## 1. Implementação
- [ ] Faz exatamente o que a etapa atual do `ROADMAP.md` pede; nada de etapas futuras.
- [ ] Está dentro de `MVP_SCOPE.md`.
- [ ] Respeita as regras de dependência (`ARCHITECTURE.md`); zona pura sem Unity.
- [ ] Nenhum número de balanceamento no código; valores em JSON validado.
- [ ] Efeitos de atributos apenas via `Balance`.
- [ ] RNG por fluxo semeado; sem relógio real na zona pura.
- [ ] Nenhuma decisão pendente tomada em silêncio; novas ambiguidades registradas em `DECISIONS.md`.

## 2. Testes
- [ ] Testes novos cobrindo o comportamento e os casos de erro, conforme `TEST_PLAN.md`.
- [ ] Testes rodam por linha de comando e passam.
- [ ] Testes com aleatoriedade usam semente explícita.
- [ ] Invariantes afetados verificados.

## 3. Integração
- [ ] Funciona com os sistemas já existentes (ex.: resultado da partida aplicado na carreira, dados carregados pelo loader real).
- [ ] Se altera o formato do save: `schemaVersion` incrementado, migração, fixture e teste.
- [ ] Contratos `MatchSetup`/`MatchResult` preservados para os dois motores.

## 4. Validação
- [ ] Executado de verdade: cenário headless, simulação em lote, temporada ou build, conforme o caso.
- [ ] Para partida: testado no editor e, nos marcos definidos, no aparelho.
- [ ] Resultado comparado aos critérios de aceite da etapa.

## 5. Performance
- [ ] Zero alocação por frame no loop da partida (verificado no Profiler quando aplicável).
- [ ] Nos marcos de performance (A7, C1, C3, C5): metas de `TECHNICAL_SPEC.md` §3 medidas nos aparelhos de referência (D-03).

## 6. Regressão
- [ ] Todos os testes anteriores continuam passando.
- [ ] Testes lentos (simulação, carreira, calibração quando existir) executados antes de considerar a etapa concluída.

## 7. Documentação
- [ ] `DECISIONS.md` atualizado se algo foi decidido ou ficou pendente.
- [ ] `CLAUDE.md` §14 (etapa atual) atualizado ao concluir uma etapa.
- [ ] Novos arquivos de dados documentados (o que controlam, faixas válidas).

## 8. Etapa concluída
Uma etapa do roadmap está concluída quando:
- [ ] Todos os critérios de aceite da etapa em `ROADMAP.md` foram verificados.
- [ ] Nada da lista "Não implementar" da etapa foi implementado.
- [ ] Todos os itens acima cumpridos para cada funcionalidade da etapa.
- [ ] Há um executável/teste demonstrando a entrega.
