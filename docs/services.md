# NotificationsAPI - Serviços de Mensageria e Envio de E-mails

Este documento detalha o funcionamento interno dos serviços da **NotificationsAPI** (`fcg-fase-02--notifications`): o worker em segundo plano `RabbitMqConsumerService`, a abstração de envio de mensagens `IEmailSender` e o padrão de tolerância a falhas.

---

## 📑 Sumário

- [1. Visão Geral da Arquitetura de Serviços](#1-visão-geral-da-arquitetura-de-serviços)
- [2. O Consumidor Assíncrono (`RabbitMqConsumerService`)](#2-o-consumidor-assíncrono-rabbitmqconsumerservice)
  - [2.1. Conexão e Declaração da Fila](#21-conexão-e-declaração-da-fila)
  - [2.2. O Ciclo de Recebimento e Confirmação Manual (BasicAck)](#22-o-ciclo-de-recebimento-e-confirmação-manual-basicack)
  - [2.3. Tratamento de Exceções e Prevenção de Loops Infinitos](#23-tratamento-de-exceções-e-prevenção-de-loops-infinitos)
- [3. O Serviço de Disparo de E-mails (`ConsoleEmailSender`)](#3-o-serviço-de-disparo-de-e-mails-consoleemailsender)
- [4. Padrão Tolerant Reader em Modelos](#4-padrão-tolerant-reader-em-modelos)
- [5. Navegação](#5-navegação)

---

## 1. Visão Geral da Arquitetura de Serviços

A NotificationsAPI foi estruturada para ser reativa e leve:
- Não possui banco de dados próprio (não armazena estado persistente de usuários).
- Mantém um canal AMQP 0-9-1 aberto escutando filas dedicadas.
- Despacha logs estruturados simulando um serviço SMTP corporativo (como SendGrid ou AWS SES).

---

## 2. O Consumidor Assíncrono (`RabbitMqConsumerService`)

Localização: `src/FCG02.NotificationsAPI.API/Services/RabbitMqConsumerService.cs`

Herda de `Microsoft.Extensions.Hosting.BackgroundService` e é registrado como um *Hosted Service* no ciclo de vida do ASP.NET Core:

### 2.1. Conexão e Declaração da Fila
1. Lê configurações de broker do `IConfiguration` (`RabbitMQ:HostName`, `RabbitMQ:Port`, etc.).
2. Abre a conexão TCP assíncrona (`CreateConnectionAsync`) e cria o canal (`CreateChannelAsync`).
3. Garante que a Exchange do tipo `Topic` e a Fila durável existam (`QueueDeclareAsync(durable: true)`).
4. Estabelece a ligação com `QueueBindAsync(queue, exchange, routingKey)`.
5. Configura `BasicQosAsync(prefetchCount: 1)` para que o worker só receba uma mensagem por vez.

### 2.2. O Ciclo de Recebimento e Confirmação Manual (BasicAck)
```csharp
var consumer = new AsyncEventingBasicConsumer(_channel);
consumer.ReceivedAsync += async (model, ea) =>
{
    try
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        
        // Processa evento e formata e-mail
        await ProcessMessageAsync(message);

        // Confirmação manual: remove permanentemente da fila
        await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Erro ao processar mensagem na fila.");
        // Rejeita sem requeue para evitar loop infinito
        await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
    }
};
```

---

## 3. O Serviço de Disparo de E-mails (`ConsoleEmailSender`)

Localização: `src/FCG02.NotificationsAPI.API/Services/ConsoleEmailSender.cs`

Implementa a interface `IEmailSender`:
```csharp
public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
```

Em ambiente local e para fins de validação acadêmica da Fase 02, o `ConsoleEmailSender` imprime no console os detalhes da notificação:
```text
============================================================
[E-MAIL ENVIADO COM SUCESSO]
Destinatário: bruce.wayne@waynecorp.com
Assunto: Bem-vindo à FIAP Cloud Games, Bruce Wayne!
Horário: 2026-10-29 10:00:00 UTC
------------------------------------------------------------
Olá Bruce Wayne,
Sua conta foi criada com sucesso na plataforma FIAP Cloud Games!
Perfil de acesso: Gamer.
============================================================
```

---

## 4. Padrão Tolerant Reader em Modelos

A NotificationsAPI define seu modelo interno de evento (`UserCreatedEvent`) independentemente dos tipos declarados na `UsersAPI`:
- Ignora campos adicionais que o produtor venha a incluir no futuro.
- Não compartilha DLLs de domínio com a `UsersAPI`, preservando a autonomia e o desacoplamento temporal dos microsserviços.

---

## 5. Navegação

- ⬅️ **[Voltar para a Arquitetura (`architecture.md`)](architecture.md)**
- 📑 **[Voltar para o Índice Central da Documentação (`index.md`)](index.md)**
