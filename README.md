# 🚗 Azure Application - Sistema Cloud Native de Locação de Carros

![Azure](https://img.shields.io/badge/azure-%230072C6.svg?style=for-the-badge&logo=microsoftazure&color=blue)
![.Net](https://img.shields.io/badge/.NET-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![C#](https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=c-sharp&logoColor=white)
![Node.js](https://img.shields.io/badge/Node.js-43853D?style=for-the-badge&logo=node.js&logoColor=white)
![GitHub Actions](https://img.shields.io/badge/github%20actions-%232671E5.svg?style=for-the-badge&logo=githubactions&logoColor=white)

Projeto focado no desenvolvimento de uma **Arquitetura de Microsserviços Orientada a Eventos (Event-Driven)** 100% Cloud Native, utilizando os recursos do **Microsoft Azure**. O sistema simula o fluxo principal de backend de uma locadora de veículos: desde o tráfego HTTP no frontend, processamento do aluguel, confirmação de pagamento, até a notificação final do cliente.

---

## 🏗️ Arquitetura do Projeto

O diagrama abaixo ilustra o fluxo de dados exato da aplicação, inspirado no desenho arquitetural do projeto. A arquitetura foi desenhada para ser altamente escalável e assíncrona, utilizando mensageria para interligar os domínios.

```mermaid
graph LR
    %% Entradas
    HTTP((Tráfego\nHTTP)) --> DNS((DNS))
    DNS --> Front[Front\nRentACar]
    Front --> BFF[BFF-RentACar\nNode]

    %% Microsserviço 1: Locação
    BFF --> RentProcess[⚡ RentProcess\nAzure Function]
    RentProcess --> RentDB[(SQL Azure\nRent DB)]
    
    %% Fila 1
    RentProcess --> PaymentQueue[[PaymentQueue\nService Bus]]

    %% Microsserviço 2: Pagamento
    PaymentQueue --> PaymentProcess[⚡ PaymentProcess\nAzure Function]
    PaymentProcess --> PaymentsStatus[(Cosmos DB\nPaymentsStatus)]
    
    %% Fila 2
    PaymentProcess --> NotificationQueue[[NotificationQueue\nService Bus]]

    %% Microsserviço 3: Notificação
    NotificationQueue --> EmailNotification[⚙️ EmailNotification\nLogic App]
    EmailNotification --> SendEmail[✉️ Send Email]
    
    %% Integrações Auxiliares
    KeyVault((KeyVault)) -.-> RentProcess
    KeyVault -.-> PaymentProcess
    AppInsights((App Insights)) -.-> RentProcess

    %% Estilização do Diagrama
    style HTTP fill:#f9f,stroke:#333
    style DNS fill:#0078D4,stroke:#fff,color:#fff
    style Front fill:#800080,stroke:#fff,color:#fff
    style BFF fill:#800080,stroke:#fff,color:#fff
    style RentProcess fill:#0072C6,stroke:#fff,color:#fff
    style RentDB fill:#0079d6,stroke:#fff,color:#fff
    style PaymentQueue fill:#D83B01,stroke:#fff,color:#fff
    style PaymentProcess fill:#0072C6,stroke:#fff,color:#fff
    style PaymentsStatus fill:#512BD4,stroke:#fff,color:#fff
    style NotificationQueue fill:#D83B01,stroke:#fff,color:#fff
    style EmailNotification fill:#0078D4,stroke:#fff,color:#fff
    style SendEmail fill:#ea4335,stroke:#fff,color:#fff
    style KeyVault fill:#ffb900,stroke:#333,color:#333
    style AppInsights fill:#0078d4,stroke:#333,color:#fff
    
```
## ⚙️ Como Funciona o Fluxo Principal

* **Entrada de Dados (Postman):** O fluxo é iniciado através de uma requisição HTTP POST (utilizando o Postman para simular o cliente), enviando o *payload* com os dados da locação diretamente para a API do backend.
* **Processamento de Locação:** A Azure Function `RentProcess` recebe a requisição, salva os dados de negócio do aluguel em um banco de dados relacional SQL Azure e despacha um evento assíncrono para a fila de pagamento.
* **Processamento de Pagamento:** A Azure Function `PaymentProcess` consome a mensagem da fila `PaymentQueue`, processa a transação, atualiza o status em um banco NoSQL Cosmos DB e envia a requisição final para a fila de notificação.
* **Notificação Automática:** O Azure Logic Apps escuta a `NotificationQueue`. Ao receber a mensagem de sucesso, o fluxo de integração visual traduz o JSON e dispara o e-mail de confirmação para o cliente.
* **Segurança e Telemetria:** Segredos de conexão são protegidos no Azure Key Vault e o desempenho é acompanhado pelo Application Insights.

---

## 🛠️ Tecnologias e Serviços Utilizados

* **Azure Functions (.NET 8):** Computação *serverless* executando regras de negócio (`RentProcess` e `PaymentProcess`).
* **Azure SQL Database:** Banco de dados relacional para persistência transacional das locações.
* **Azure Cosmos DB:** Banco de dados não relacional escalável para status de pagamentos.
* **Azure Service Bus:** Barramento de mensageria garantindo o desacoplamento entre os serviços.
* **Azure Logic Apps:** Solução *low-code* para envio automatizado de e-mails.
* **GitHub Actions:** Automação de CI/CD para compilação e deploy.
* **Postman:** Ferramenta utilizada para simular as requisições HTTP e testar as APIs.

---

## 🚧 Desafios Enfrentados e Soluções Técnicas

Durante o desenvolvimento deste projeto, superamos cenários reais de DevOps e arquitetura Cloud:

### 1. Limitações e Contornos no Azure Free Tier
* **Desafio:** Otimizar a arquitetura para rodar com alta disponibilidade sem esgotar os créditos gratuitos de estudante.
* **Solução:** Ajuste fino no *polling interval* (tempo de verificação) do Logic Apps para não consumir execuções ociosas. Provisionamento do Cosmos DB e do SQL Azure em camadas de custo-benefício adequadas ao ambiente de dev/teste.

### 2. Falhas de Diretório no Pipeline CI/CD (MSB1003)
* **Desafio:** O GitHub Actions falhava ao tentar compilar a função com o erro `MSB1003`, pois o *runner* não encontrava o arquivo `.csproj` no repositório.
* **Solução:** Configuração explícita do mapa de diretórios no arquivo YAML utilizando a variável `AZURE_FUNCTIONAPP_PACKAGE_PATH` e o comando *shell* `pushd`. O *build* foi alterado para `dotnet publish` para empacotar corretamente as dependências do SDK do Azure.

### 3. Proteção de Dados Sensíveis (Secret Scanning)
* **Desafio:** Ao exportar o ambiente do Azure via Infraestrutura como Código (ARM Templates) e tentar fazer o *push*, o GitHub bloqueou a ação por violação de segurança (chaves reais expostas no código).
* **Solução:** Sanitização completa do arquivo `template.json`. As *connection strings* reais (como a `SharedAccessKey` do Service Bus) foram substituídas pelo uso seguro do tipo `SecureString` no bloco `parameters`. O histórico sujo do Git foi reescrito via `git reset --soft origin/main` para garantir total conformidade de segurança e anonimização do Tenant ID.

---

## 🚀 Como fazer o Deploy Local

Se desejar replicar este ambiente de estudo, siga os passos abaixo:

1. **Clone este repositório:**
   ```bash
   git clone [https://github.com/ErickGeovane0706/Azure-Aplicacao-Aluguel-de-Carros.git](https://github.com/ErickGeovane0706/Azure-Aplicacao-Aluguel-de-Carros.git)
