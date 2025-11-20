# RabbitMQ s STOMP protokolem v .NET Aspire

Tento projekt demonstruje použití RabbitMQ s STOMP protokolem v .NET Aspire aplikaci.

## Struktura projektu

### 1. **MessagingAspire.AppHost**
Aspire AppHost projekt, který orchestruje všechny služby:
- RabbitMQ s povoleným STOMP a Web STOMP pluginem
- Management plugin pro webové rozhraní
- Porty:
  - **5672** - AMQP protokol
  - **15672** - Management UI
  - **61613** - STOMP protokol
  - **15674** - STOMP WebSocket

### 2. **MessagingAspire.ApiService**
API service s endpointem pro posílání zpráv do RabbitMQ:
- `POST /messages` - odešle zprávu do RabbitMQ fronty
- Používá RabbitMQ.Client pro AMQP komunikaci
- Zprávy se posílají do exchange `chat.exchange` s routing key `chat.message`

### 3. **MessagingAspire.Consumer**
Worker Service, který konzumuje zprávy z RabbitMQ:
- Připojuje se k frontě `chat.queue`
- Zpracovává zprávy asynchronně
- Loguje přijaté zprávy

### 4. **MessagingAspire.Web**
Blazor Web aplikace s chat rozhraním:
- **/chat** - jednoduchá chat stránka (lokální echo)
- **/stomp-chat** - plně funkční STOMP WebSocket chat
- JavaScript STOMP klient (`stomp-client.js`)

## Jak to funguje

### AMQP komunikace (ApiService → RabbitMQ → Consumer)

1. **Producer (ApiService)**:
```csharp
// Vytvoří channel a deklaruje exchange a frontu
await using var channel = await connection.CreateChannelAsync();
await channel.ExchangeDeclareAsync(exchange: "chat.exchange", type: ExchangeType.Topic);
await channel.QueueDeclareAsync(queue: "chat.queue", durable: true);
await channel.QueueBindAsync(queue: "chat.queue", exchange: "chat.exchange", routingKey: "chat.#");

// Odešle zprávu
var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
await channel.BasicPublishAsync(exchange: "chat.exchange", routingKey: "chat.message", body: body);
```

2. **Consumer (Worker Service)**:
```csharp
var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (sender, ea) => {
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    logger.LogInformation("Received: {Message}", message);
    await channel.BasicAckAsync(ea.DeliveryTag, false);
};
await channel.BasicConsumeAsync(queue: "chat.queue", autoAck: false, consumer: consumer);
```

### STOMP komunikace (Web Browser ↔ RabbitMQ)

STOMP (Simple Text Oriented Messaging Protocol) umožňuje přímou komunikaci z webového prohlížeče přes WebSocket.

**Připojení k STOMP:**
```javascript
const ws = new WebSocket('ws://localhost:15674/ws');
// Odeslat CONNECT frame
ws.send('CONNECT\naccept-version:1.2\n\n\x00');
```

**Subscribe na frontu:**
```javascript
ws.send('SUBSCRIBE\nid:sub-0\ndestination:/exchange/chat.exchange/chat.#\n\n\x00');
```

**Odeslání zprávy:**
```javascript
const body = JSON.stringify({User: "Jan", Text: "Ahoj", Timestamp: new Date()});
ws.send('SEND\ndestination:/exchange/chat.exchange/chat.message\n\n' + body + '\x00');
```

## Spuštění aplikace

1. **Spusťte AppHost projekt:**
```bash
cd MessagingAspire.AppHost
dotnet run
```

2. **Otevřete Aspire Dashboard:**
   - Automaticky se otevře na `https://localhost:17XXX`
   - Můžete sledovat všechny služby, logy a metriky

3. **Otevřete webovou aplikaci:**
   - Najděte endpoint pro `webfrontend` v Aspire Dashboard
   - Přejděte na `/stomp-chat`

4. **RabbitMQ Management UI:**
   - Najděte endpoint pro `rabbitmq` v Aspire Dashboard
   - Port 15672 - Management UI
   - Přihlašovací údaje: `guest` / `guest`

## Testování

### Test 1: AMQP komunikace (API → Consumer)

Použijte HTTP klienta (curl, Postman, nebo .http soubor):

```http
POST https://localhost:7XXX/messages
Content-Type: application/json

{
  "user": "TestUser",
  "text": "Hello from API!"
}
```

Zkontrolujte logy v Consumer projektu - měla by se zobrazit přijatá zpráva.

### Test 2: STOMP WebSocket chat

1. Otevřete `/stomp-chat` ve webovém prohlížeči
2. Zadejte své jméno
3. Nastavte STOMP URL (výchozí: `ws://localhost:15674/ws`)
4. Klikněte na "Connect to STOMP"
5. Pošlete zprávu
6. Otevřete další okno prohlížeče a opakujte kroky - obě okna by měla vidět zprávy

### Test 3: Kombinovaný test

1. Pošlete zprávu přes API endpoint
2. Zpráva by se měla zobrazit:
   - V logu Consumer projektu (AMQP)
   - Ve všech připojených STOMP klientech (WebSocket)

## Konfigurace RabbitMQ

V `AppHost.cs` je RabbitMQ nakonfigurován s následujícími pluginy:

```csharp
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()  // Web UI
    .WithEnvironment("RABBITMQ_PLUGINS", 
        "rabbitmq_management rabbitmq_stomp rabbitmq_web_stomp");
```

### Důležité environment proměnné:
- `RABBITMQ_PLUGINS` - seznam povolených pluginů
- `rabbitmq_stomp` - STOMP protokol na portu 61613
- `rabbitmq_web_stomp` - STOMP přes WebSocket na portu 15674

## Topologie zpráv

```
┌─────────────┐
│   Producer  │
│ (ApiService)│
└──────┬──────┘
       │
       │ AMQP (5672)
       ▼
┌─────────────────┐
│   RabbitMQ      │
│                 │
│  chat.exchange  │◄─── STOMP WebSocket (15674)
│  (Topic)        │
│       │         │
│       ▼         │
│  chat.queue     │
└─────┬───────────┘
      │
      ├─── AMQP ───► Consumer (Worker)
      │
      └─── STOMP ──► Web Browser Clients
```

## Formát zpráv

Všechny zprávy jsou ve formátu JSON:

```json
{
  "Id": "uuid",
  "User": "jméno uživatele",
  "Text": "text zprávy",
  "Timestamp": "2025-11-20T13:20:00Z"
}
```

## Řešení problémů

### STOMP se nepřipojí
- Zkontrolujte, že RabbitMQ container běží
- Ověřte port 15674 v Aspire Dashboard
- Zkontrolujte konzoli prohlížeče pro WebSocket chyby

### Consumer nepřijímá zprávy
- Zkontrolujte logy Consumer projektu
- Ověřte, že fronta `chat.queue` existuje v RabbitMQ Management UI
- Zkontrolujte binding mezi exchange a frontou

### Management UI není dostupné
- Zkontrolujte port 15672 v Aspire Dashboard
- Výchozí přihlašovací údaje: `guest`/`guest`

## Další vylepšení

- Přidat autentizaci pro STOMP klienty
- Implementovat heartbeat pro STOMP připojení
- Přidat persistence zpráv
- Implementovat historii chatu
- Přidat typing indicators
- Implementovat private messages (routing keys)
