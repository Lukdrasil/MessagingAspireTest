# ✅ TESTOVÁNÍ - Vaše aplikace běží!

## 🚀 Aplikace je spuštěná na: https://localhost:17276

---

## 🧪 TEST #1: STOMP WebSocket Chat (5 minut)

### Krok 1: Otevřete Web aplikaci

1. **Otevřete Aspire Dashboard**: https://localhost:17276/login?t=f28fefb5d9aec92539c8f03940f43a40
2. V Dashboard najděte službu **"webfrontend"**
3. Klikněte na HTTPS endpoint (např. `https://localhost:7XXX`)
4. Blazor aplikace se otevře

### Krok 2: Připojte se k STOMP

1. V navigaci klikněte **"STOMP Chat"**
2. Vyplňte:
   - **Your Name**: Test1
   - **STOMP URL**: ws://localhost:15674/ws (už by mělo být vyplněné)
3. Klikněte **"Connect to STOMP"**
4. Status by měl změnit na **"Connected"** (zelený badge)

### Krok 3: Pošlete zprávu

1. Do pole "Message" napište: `Hello from STOMP!`
2. Klikněte **"Send"** nebo stiskněte Enter
3. ✅ **Zpráva by se měla zobrazit v chat okně**

### Krok 4: Test více klientů

1. Otevřete **nový tab/okno** prohlížeče
2. Přejděte na stejnou URL
3. Připojte se jako **"Test2"**
4. Pošlete zprávu z Test2
5. ✅ **Zpráva by se měla zobrazit v OBOU oknech!**

---

## 🧪 TEST #2: Ověření Consumer logů (2 minuty)

### Kroky:

1. V Aspire Dashboard klikněte na záložku **"Logs"** (vlevo)
2. V dropdown menu vyberte **"consumer"**
3. Sledujte výstup

### Co byste měli vidět:

```
✅ "Starting RabbitMQ Consumer..."
✅ "Consumer is now listening for messages..."
✅ Po odeslání zprávy:
   "Received message: {\"User\":\"Test1\",\"Text\":\"Hello from STOMP!\",...}"
```

---

## 🧪 TEST #3: API Endpoint (3 minuty)

### Zjistěte API URL:

1. V Aspire Dashboard najděte **"apiservice"**
2. Zkopírujte **HTTPS endpoint** (např. `https://localhost:7123`)

### Spusťte PowerShell test:

```powershell
# Vložte svůj API endpoint
$apiUrl = "https://localhost:7123"  # <-- ZMĚŇTE PORT!

$body = @{
    user = "APITest"
    text = "Hello from API at $(Get-Date -Format 'HH:mm:ss')"
} | ConvertTo-Json

Invoke-RestMethod -Uri "$apiUrl/messages" `
    -Method Post `
    -Body $body `
    -ContentType "application/json" `
    -SkipCertificateCheck
```

### Očekávaný výsledek:

```json
✅ Response:
{
  "success": true,
  "message": {
    "id": "...",
    "user": "APITest",
    "text": "Hello from API at 13:45:22",
    "timestamp": "2025-11-20T..."
  }
}
```

### Ověření:

1. **V STOMP Chat** - zpráva by se měla zobrazit!
2. **V Consumer logu** - zpráva by měla být zalogována
3. ✅ **Toto dokazuje, že AMQP → RabbitMQ → STOMP funguje!**

---

## 🧪 TEST #4: RabbitMQ Management (2 minuty)

### Kroky:

1. V Aspire Dashboard najděte **"rabbitmq"**
2. Klikněte na endpoint s portem **15672**
3. Přihlaste se:
   - Username: `guest`
   - Password: `guest`

### Co zkontrolovat:

1. **Exchanges** tab → klikněte na `chat.exchange`
   - ✅ Type: topic
   - ✅ V sekci Bindings vidíte vazbu na `chat.queue`

2. **Queues** tab → klikněte na `chat.queue`
   - ✅ Consumers: 1 (Worker Service)
   - ✅ Bindings obsahuje `chat.exchange`

3. **Connections** tab
   - ✅ Vidíte připojení od ApiService, Consumer, a STOMP klientů

---

## 🎯 KOMPLEXNÍ END-TO-END TEST

Tento test ověří celý message flow:

### Scénář:

1. **Otevřete 2 STOMP Chat okna** (Test1, Test2)
2. **Oba připojte** k STOMP
3. **Test1 pošle zprávu**: "Hello from browser"
4. **Spusťte API request** z PowerShell: "Hello from API"

### Očekávaný výsledek:

```
✅ OBĚ zprávy se zobrazí v OBOU STOMP chat oknech
✅ Consumer log ukáže OBĚ zprávy
✅ RabbitMQ Management → Queues → chat.queue → Message rates graf ukazuje aktivitu
```

---

## 📊 Checklist úspěšného testu

Po dokončení všech testů byste měli mít:

```
✅ Aspire Dashboard ukazuje všechny služby jako "Running"
✅ STOMP Chat se úspěšně připojuje a posílá zprávy
✅ Zprávy se zobrazují real-time ve všech připojených klientech
✅ API endpoint úspěšně přijímá a zpracovává zprávy
✅ Consumer loguje každou přijatou zprávu
✅ RabbitMQ Management UI ukazuje správnou topologii
✅ Multi-client chat funguje bez problémů
```

---

## 🐛 Pokud něco nefunguje

### STOMP se nepřipojí?

1. Otevřete DevTools (F12) → Console
2. Hledejte WebSocket chyby
3. Zkontrolujte URL: `ws://localhost:15674/ws`
4. V Aspire Dashboard ověřte, že rabbitmq běží

### Consumer nepřijímá zprávy?

1. Aspire Dashboard → Logs → consumer
2. Zkontrolujte, že vidíte "Consumer is now listening..."
3. RabbitMQ Management → Queues → ověřte Consumers count

### API vrací chybu?

1. Aspire Dashboard → Logs → apiservice
2. Zkontrolujte stack trace
3. Ověřte správný formát JSON

---

## 🎉 Hotovo!

Aplikace je plně funkční a používá:
- ✅ **RabbitMQ** s STOMP protokolem
- ✅ **AMQP** pro backend komunikaci
- ✅ **WebSocket STOMP** pro real-time chat
- ✅ **.NET Aspire** pro orchestraci
- ✅ **Blazor** pro UI

**Všechny 3 komponenty (API, Consumer, STOMP) komunikují přes RabbitMQ!** 🚀
