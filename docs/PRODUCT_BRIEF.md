# FOOTBALL NATION — ESPECIFICAÇÃO DO PRODUTO

## 1. Identidade

Nome oficial: Football Nation.

Categoria: jogo de futebol 3D com carreira de treinador e gestão de clube.

Plataforma inicial: Android.

Tecnologia de referência: Unity 6 LTS e C#.

Idioma inicial: português brasileiro (pt-BR).

Modo de funcionamento: totalmente offline.

Referência de experiência: Dream League Soccer, utilizado como referência de acessibilidade, partidas de futebol em tempo real, controles intuitivos e evolução de elenco. Não copiar recursos protegidos, identidade visual, código ou conteúdo proprietário.

## 2. Conceito

Football Nation é um jogo de futebol no qual o jogador assume o papel de treinador e conduz um clube desde as divisões inferiores até a elite do futebol brasileiro.

O jogador poderá criar seu próprio clube, personalizar sua identidade, disputar partidas 3D curtas, montar escalações, treinar atletas, negociar jogadores no mercado nacional e construir uma trajetória por várias temporadas.

A experiência deve equilibrar futebol jogável e gestão de carreira. A interface deve ser acessível, a progressão deve ser satisfatória e as partidas devem ser divertidas sem exigir longas sessões de jogo.

## 3. Identidade visual

A direção visual deve combinar a personalidade do futebol brasileiro com acabamento moderno e qualidade de apresentação de um jogo esportivo comercial.

Paleta inicial:
- Verde profundo: #146B43.
- Amarelo dourado: #F5C542.
- Azul: #174A8B.
- Branco: #F7F8FA.

Utilizar verde e tons neutros como base, dourado para destaques e azul como cor complementar. Evitar saturação excessiva, interfaces visualmente carregadas e uso indiscriminado das quatro cores.

O logotipo deve ser legível em tamanhos pequenos, funcionar como ícone Android e permitir aplicação em telas, uniformes e materiais promocionais.

Desenvolver um escudo moderno com linguagem esportiva, formas geométricas e referências sutis ao futebol brasileiro. Não reproduzir escudos de clubes existentes nem utilizar símbolos oficiais sem autorização.

O nome deve aparecer como FOOTBALL NATION, mantendo a mesma grafia em telas, documentação, identificadores visuais e materiais do produto. Identificadores técnicos internos podem seguir as convenções existentes do projeto até que uma migração seja necessária.

## 4. Gameplay

O jogo deve apresentar partidas 3D em tempo real, com:
- Movimentação dos jogadores.
- Controle de bola.
- Passes e finalizações.
- Corrida e desarmes.
- Posicionamento ofensivo e defensivo.
- Goleiros e defesas.
- Gols, placar, reinícios e encerramento.
- Câmera apropriada para dispositivos móveis.
- Inteligência artificial para companheiros e adversários.

A duração deve ser configurável, com opções iniciais de 3, 4 e 5 minutos para testes.

A prioridade é produzir partidas responsivas, compreensíveis e divertidas, com comportamento consistente da bola e dos jogadores.

## 5. Controles e dificuldade

Oferecer configuração padrão simples e opções para personalizar os controles.

Permitir ajustes de posição e tamanho dos botões, mapeamento dos comandos e sensibilidade quando aplicável.

A dificuldade deve modificar efetivamente o comportamento da inteligência artificial e o desafio apresentado ao jogador.

As configurações devem funcionar em diferentes tamanhos de tela e não podem depender de conexão com a internet.

## 6. Carreira de treinador

A carreira deve permitir:
- Criar ou escolher um clube.
- Montar e editar escalações.
- Definir formações e estratégias.
- Disputar partidas jogáveis.
- Simular os demais jogos.
- Acompanhar calendário e classificação.
- Treinar jogadores.
- Negociar transferências.
- Administrar contratos e orçamento.
- Avançar entre temporadas.
- Conquistar acessos e enfrentar rebaixamentos.

O ciclo principal deve ser: preparar a equipe, jogar ou simular uma partida, avaliar o resultado, administrar o elenco e avançar a temporada.

## 7. Clubes e campeonatos

Implementar uma estrutura configurável das Séries D, C, B e A.

Quantidade de clubes, formato dos campeonatos, calendário, critérios de desempate e regras de acesso e rebaixamento devem ser configuráveis.

O sistema deve manter consistência entre calendário, resultados, classificação e progressão dos clubes.

Na primeira versão, utilizar clubes fictícios ou editáveis, com suporte à criação de um clube próprio.

## 8. Treinamento e Overall

O treinamento deve desenvolver atributos individuais e permitir a evolução do Overall.

Considerar posição, idade, potencial, frequência de treino, intensidade, fadiga e recuperação.

O Overall deve derivar dos atributos relevantes para cada posição, seguindo regras consistentes e testáveis.

A evolução deve ter limites e equilíbrio, evitando progressão ilimitada ou aumentos sem relação com o treinamento.

## 9. Mercado de transferências

A primeira versão deve limitar as transferências ao mercado brasileiro.

Implementar busca de jogadores, ofertas, contrapropostas, valores de transferência, salários, contratos e orçamento.

A contratação deve atualizar o elenco e as finanças de maneira consistente.

Jogadores fictícios podem ser gerados localmente para manter o mercado funcional ao longo de várias temporadas.

Não depender de APIs externas ou serviços online.

## 10. Funcionamento offline

Todos os sistemas essenciais devem funcionar sem internet, incluindo gameplay, carreira, campeonatos, mercado, treinamento, simulação e salvamento.

Os dados necessários devem estar disponíveis localmente.

Implementar salvamento versionado, validação de dados, tratamento de falhas e mecanismos de recuperação quando viáveis.

## 11. Qualidade e desempenho

Buscar apresentação 3D realista, com modelos, animações, iluminação, materiais, estádio e câmera coerentes com um jogo esportivo comercial.

Priorizar desempenho estável, controles responsivos e uso eficiente de memória e processamento no Android.

Prever níveis gráficos ajustáveis e medir o desempenho em dispositivos reais. Não considerar a qualidade visual concluída apenas pela aparência em uma cena de desenvolvimento.

## 12. Princípios de desenvolvimento

A implementação deve ser modular, testável e integrada.

Preservar o código original durante a reconstrução. Reaproveitar componentes somente após verificar sua qualidade e compatibilidade.

Desenvolver em etapas funcionais, começando por uma partida jogável e evoluindo para a carreira integrada.

Executar compilações, testes automatizados e verificações de integração. Não declarar funcionalidades concluídas sem evidências de validação.

## 13. Critério de sucesso

A primeira versão integrada deve permitir criar um clube, iniciar uma carreira offline, montar uma equipe, jogar partidas 3D, treinar jogadores, realizar transferências nacionais e avançar por campeonatos e temporadas.

A qualidade final deve ser avaliada pela combinação de diversão, clareza, consistência dos sistemas, desempenho e facilidade de uso.