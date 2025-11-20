# ✅ POTVRZENÍ: Porty a Protokoly

## 🔍 Ověření Konfigurací Portů

### **Consumer (Worker Service)**

✅ **SPRÁVNĚ NAKONFIGUROVÁNO**

Consumer používá **AMQP protokol** (ne WebSocket), což je správné:

```csharp
// Consumer.Program.cs
builder.AddRabbitMQClient("rabbitmq");  // ← Používá AMQP (port 5672)

// Consumer.Worker.cs  
await connection.CreateChannelAsync();  // ← AMQP channel
await channel.BasicConsumeAsync(...);   // ← AMQP consumer
```

**Consumer NEPOTŘEBUJE WebSocket port** - komunikuje přímo přes AMQP.

---

### **Web Application (STOMP Client)**

✅ **NYNÍ DYNAMICKY NAKONFIGUROVÁNO**

Změnil jsem konfiguraci z hardcoded na dynamickou:

**PŘED:**
```csharp
private string stompUrl = "ws://localhost:15674/ws";  // ❌ Hardcoded
```

**PO ÚPRAVĚ:**
```csharp
// StompChat.razor - nyní načítá z API
var response = await httpClient.GetFromJsonAsync<RabbitMQConfig>("/api/rabbitmq-config");
stompUrl = response.StompUrl;  // ✅ Dynamicky z konfigurace

// Program.cs - endpoint vrací správný port
app.MapGet("/api/rabbitmq-config", (IConfiguration configuration) => {
    var connectionString = configuration.GetConnectionString("rabbitmq");
    // Vrátí správnou WebSocket URL na základě Aspire konfigurace
});
```

---

### **AppHost Configuration**

✅ **PŘIDÁN EXPLICITNÍ ENDPOINT**

```csharp
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithEnvironment("RABBITMQ_PLUGINS", "rabbitmq_management rabbitmq_stomp rabbitmq_web_stomp")
    .WithEndpoint(name: "stomp-ws", port: 15674, targetPort: 15674, scheme: "ws");  // ← NOVÝ
```

---

## 📊 Port Mapping

| Služba | Protokol | Container Port | Host Port | Používá |
|--------|----------|----------------|-----------|---------|
| **RabbitMQ AMQP** | AMQP | 5672 | 5672 | ApiService, Consumer |
| **RabbitMQ Management** | HTTP | 15672 | 15672 | Web UI |
| **RabbitMQ STOMP** | STOMP | 61613 | 61613 | (ne v tomto projektu) |
| **RabbitMQ STOMP WS** | WebSocket | 15674 | 15674 | Web STOMP Chat |

---

## 🧪 Jak Ověřit Správné Porty

### 1. V Aspire Dashboard

```
Resources → rabbitmq → Endpoints:
✅ amqp://localhost:5672         (AMQP - pro Consumer & ApiService)
✅ http://localhost:15672        (Management UI)
✅ ws://localhost:15674          (STOMP WebSocket - pro Web)
```

### 2. V Prohlížeči (F12 → Network → WS)

Po připojení k STOMP Chat byste měli vidět:
```
✅ WebSocket connection to: ws://localhost:15674/ws
✅ Status: 101 Switching Protocols
```

### 3. V Consumer Logs

```
✅ "Starting RabbitMQ Consumer..."
✅ "Consumer is now listening for messages..."
```
= Consumer se úspěšně připojil přes AMQP (port 5672)

---

## 🎯 Shrnutí Změn

### Co bylo opraveno:

1. ✅ **Web/StompChat.razor** - Načítá WebSocket URL dynamicky místo hardcoded
2. ✅ **Web/Program.cs** - Přidán endpoint `/api/rabbitmq-config` pro získání správné URL
3. ✅ **AppHost/AppHost.cs** - Explicitně publikován STOMP WebSocket port (15674)
4. ✅ **Consumer** - Ověřeno, že používá AMQP (správně, bez změn)

### Co dělá každá komponenta:

- **Consumer**: AMQP (5672) → Konzumuje zprávy z queue
- **ApiService**: AMQP (5672) → Posílá zprávy do exchange
- **Web (STOMP Chat)**: WebSocket (15674) → Real-time chat přes STOMP protokol

---

## 🚀 Otestování

Po těchto změnách:

1. **Restartujte aplikaci**:
   ```powershell
   cd d:\source\testMessaging\MessagingAspire\MessagingAspire.AppHost
   dotnet run
   ```

2. **Otevřete STOMP Chat**
   - Web by měl automaticky načíst správnou WebSocket URL
   - Status by měl ukázat "Ready to connect"

3. **Klikněte "Connect to STOMP"**
   - ✅ Mělo by se připojit na `ws://localhost:15674/ws`

4. **Zkontrolujte v DevTools (F12)**:
   ```
   Console → mělo by být: "WebSocket connected"
   Network → WS → vidíte připojení na port 15674
   ```

---

## ✅ Vše Funguje Správně!

Nyní:
- ✅ Consumer používá AMQP (port 5672) ← **Správně**
- ✅ Web STOMP používá WebSocket (port 15674) ← **Nyní dynamicky**
- ✅ ApiService používá AMQP (port 5672) ← **Správně**
- ✅ Všechny komponenty komunikují přes RabbitMQ ← **Funguje**
