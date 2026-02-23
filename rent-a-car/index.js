const express = require('express');
const cors = require('cors');
const { ServiceBusClient } = require('@azure/service-bus');
require('dotenv').config();

const app = express();
app.use(cors());
app.use(express.json());

app.post('/api/locacao', async (req, res) => {
    // 1. Pega os dados da requisição
    const { nome, email, modelo, ano, tempoAluguel } = req.body;
    
    // 2. Monta o payload 
    const mensagem = {
        nome,
        email,
        modelo,
        ano,
        tempoAluguel,
        data: new Date().toISOString(),
    };

    try {
        // 3. Pega a string de conexão e o nome da fila do arquivo .env
        const serviceBusConnectionString = process.env.SERVICE_BUS_CONNECTION_STRING;
        const queueName = process.env.NOME_DA_FILA; // Agora puxa dinamicamente do .env!

        // 4. Inicia o cliente do Service Bus
        const sbClient = new ServiceBusClient(serviceBusConnectionString);
        const sender = sbClient.createSender(queueName);

        // 5. Estrutura a mensagem para o Azure
        const messageToAzure = {
            body: mensagem, // Corrigido de mensagemBody para mensagem
            contentType: 'application/json',
            subject: "locacao", 
        };

        // 6. Envia e fecha as conexões
        await sender.sendMessages(messageToAzure);
        await sender.close();
        await sbClient.close();
        
        console.log(`✅ Mensagem enviada para a fila com sucesso: ${nome}`);
        res.status(201).json({ message: 'Locação registrada e mensagem enviada para o Service Bus' });        

    } catch (error) {
        console.error("❌ ERRO REAL NO SERVICE BUS:", error); 
        return res.status(500).json({ error: 'Erro ao enviar mensagem para o Service Bus' });
    }
});

app.listen(3001, () => {
    console.log('🚀 Servidor rodando na porta 3001');
});