# Rychlý průvodce spuštěním

## Krok 1: Spuštění aplikace

```bash
cd MessagingAspire.AppHost
dotnet run
```

Nebo použijte VS Code / Visual Studio:
- Otevřete projekt v IDE
- Nastavte `MessagingAspire.AppHost` jako startup projekt
- Stiskněte F5

## Krok 2: Ověření služeb

Po spuštění se otevře **Aspire Dashboard**. Zkontrolujte, že běží:

✅ **rabbitmq** - RabbitMQ server
✅ **apiservice** - API Service 
✅ **consumer** - Message Consumer
✅ **webfrontend** - Blazor Web App

## Krok 3: Přístup k RabbitMQ Management

1. V Aspire Dashboard najděte **rabbitmq**
2. Klikněte na endpoint pro port **15672**
3. Přihlaste se:
   - Username: `guest`
   - Password: `guest`
4. V sekci **Exchanges** by měl být `chat.exchange`
5. V sekci **Queues** by měla být `chat.queue`

## Krok 4: Test STOMP Chatu

### Varianta A: Přes Web UI

1. V Aspire Dashboard najděte **webfrontend**
2. Klikněte na HTTPS endpoint
3. V navigaci klikněte na **STOMP Chat**
4. Zadejte své jméno (např. "Jan")
5. STOMP URL by měla být předvyplněná (např. `ws://localhost:15674/ws`)
6. Klikněte **Connect to STOMP**
7. Jakmile se připojíte, napište zprávu a klikněte **Send**

### Testování s více klienty:
- Otevřete stejnou stránku v jiném okně/tabu
- Připojte se s jiným jménem
- Pošlete zprávy - měly by se zobrazit ve všech připojených oknech!

### Varianta B: Přes API

Použijte `test-api.http` soubor nebo curl:

```bash
curl -X POST https://localhost:7XXX/messages \
  -H "Content-Type: application/json" \
  -d '{"user":"TestUser","text":"Hello from curl!"}'
```

Zpráva by se měla:
1. Objevit v logu **consumer** projektu
2. Zobrazit ve všech připojených STOMP klientech

## Krok 5: Sledování zpráv

### V Aspire Dashboard:
- Přepněte na **Logs** tab
- Vyberte **consumer** projekt
- Měli byste vidět přijímané zprávy

### V RabbitMQ Management:
- Exchanges → `chat.exchange` → klikněte na jméno
- V sekci **Bindings** by měl být binding na `chat.queue`
- Queues → `chat.queue` → vidíte statistiky zpráv

## Řešení běžných problémů

### ❌ RabbitMQ container se nespustí
**Řešení:**
- Zkontrolujte, že Docker běží
- Zkontrolujte dostupné porty: 5672, 15672, 61613, 15674
- Zkuste restartovat Docker Desktop

### ❌ STOMP se nepřipojí
**Řešení:**
- Zkontrolujte port v URL (15674)
- Otevřete Developer Tools (F12) → Console
- Zkontrolujte WebSocket chyby
- Ověřte, že RabbitMQ běží a má povolený web_stomp plugin

### ❌ Consumer nepřijímá zprávy
**Řešení:**
- Zkontrolujte logy v Aspire Dashboard
- Ověřte, že `chat.queue` existuje v RabbitMQ Management
- Zkontrolujte binding mezi exchange a queue

### ❌ API vrací chybu při posílání zprávy
**Řešení:**
- Zkontrolujte, že ApiService má připojení k RabbitMQ
- Podívejte se do logů ApiService
- Ověřte formát JSON requestu

## Užitečné URL (výchozí porty)

Po spuštění zkontrolujte skutečné porty v Aspire Dashboard!

- **Aspire Dashboard**: `https://localhost:17XXX`
- **RabbitMQ Management**: `http://localhost:15672`
- **RabbitMQ AMQP**: `amqp://localhost:5672`
- **RabbitMQ STOMP**: `stomp://localhost:61613`
- **RabbitMQ STOMP WebSocket**: `ws://localhost:15674/ws`
- **Web Frontend**: `https://localhost:7XXX`
- **API Service**: `https://localhost:7XXX`

## Příklad flow zprávy

```
[Browser STOMP Client]
        │
        │ ws://localhost:15674/ws
        ▼
    [RabbitMQ]
        │
        ├──► [chat.exchange] (Topic)
        │           │
        │           │ routing key: chat.#
        │           ▼
        │    [chat.queue]
        │           │
        │           ├──► [Consumer Worker] (AMQP)
        │           │         └─► Logs zprávu
        │           │
        │           └──► [STOMP Clients] (WebSocket)
        │                     └─► Zobrazí zprávu
        │
[HTTP POST /messages]
        │
    [ApiService]
        └──► [RabbitMQ] (AMQP)
```

## Co dál?

- Experimentujte s různými routing keys
- Přidejte více consumers
- Vyzkoušejte RabbitMQ Management UI
- Prohlédněte si metriky v Aspire Dashboard
- Upravte exchange typ (topic → direct/fanout)
