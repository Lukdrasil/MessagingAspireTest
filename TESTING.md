# 🧪 Testovací Checklist - RabbitMQ STOMP Aplikace

## ✅ Krok za krokem testování

### 1️⃣ Ověření spuštění aplikace

**Akce:**
```powershell
cd d:\source\testMessaging\MessagingAspire\MessagingAspire.AppHost
dotnet run
```

**Očekávaný výsledek:**
```
✅ "Now listening on: https://localhost:17XXX"
✅ "Login to the dashboard at https://localhost:17XXX/login?t=..."
```

---

### 2️⃣ Kontrola služeb v Aspire Dashboard

**Akce:**
1. Otevřete URL z terminálu (https://localhost:17XXX/login?t=...)
2. V dashboardu zkontrolujte sekci **Resources**

**Očekávaný výsledok:**
```
✅ rabbitmq       - Running (zelený stav)
✅ apiservice     - Running  
✅ consumer       - Running
✅ webfrontend    - Running
```

**Pokud některá služba není Running:**
- Klikněte na službu → záložka **Logs**
- Zkontrolujte chybové hlášky

---

### 3️⃣ Test RabbitMQ Management UI

**Akce:**
1. V Aspire Dashboard najděte **rabbitmq**
2. Klikněte na endpoint s portem **15672**
3. Přihlaste se:
   - Username: `guest`
   - Password: `guest`

**Očekávaný výsledek:**
```
✅ Přihlášení úspěšné
✅ V záložce "Exchanges" existuje: chat.exchange
✅ V záložce "Queues" existuje: chat.queue
✅ V "Queues" → klikněte na "chat.queue" → vidíte Bindings
```

---

### 4️⃣ Test STOMP WebSocket Chat (HLAVNÍ TEST)

**Akce:**
1. V Aspire Dashboard najděte **webfrontend**
2. Klikněte na HTTPS endpoint (např. https://localhost:7xxx)
3. V navigaci klikněte **"STOMP Chat"**
4. Vyplňte formulář:
   - Your Name: `Tester1`
   - STOMP URL: `ws://localhost:15674/ws` (předvyplněno)
5. Klikněte **"Connect to STOMP"**

**Očekávaný výsledek:**
```
✅ Status badge změní na "Connected" (zelený)
✅ Zobrazí se zpráva: "Successfully connected to STOMP server"
```

**Nyní pošlete zprávu:**
6. Do pole Message napište: `Hello from STOMP!`
7. Klikněte **"Send"**

**Očekávaný výsledek:**
```
✅ Zpráva se zobrazí v chat okně
✅ Formát: [Tester1] (13:45:30) Hello from STOMP!
```

**Multi-client test:**
8. Otevřete NOVÉ okno prohlížeče (nebo incognito mode)
9. Přejděte na stejnou URL: https://localhost:7xxx/stomp-chat
10. Připojte se jako `Tester2`
11. Pošlete zprávu: `Hi from second client!`

**Očekávaný výsledek:**
```
✅ Zpráva z Tester2 se zobrazí v OBOU oknech prohlížeče
✅ Zprávy z Tester1 se také zobrazí v okně Tester2
```

---

### 5️⃣ Test API Endpoint

**Akce:**
1. V Aspire Dashboard najděte **apiservice**
2. Zkopírujte HTTPS endpoint (např. `https://localhost:7123`)
3. Otevřete PowerShell:

```powershell
# Nahraďte URL vaším endpointem
$apiUrl = "https://localhost:7123"

$body = @{
    user = "APITester"
    text = "Test message from API"
} | ConvertTo-Json

Invoke-RestMethod -Uri "$apiUrl/messages" `
    -Method Post `
    -Body $body `
    -ContentType "application/json" `
    -SkipCertificateCheck
```

**Očekávaný výsledek:**
```json
✅ Response:
{
  "success": true,
  "message": {
    "id": "guid...",
    "user": "APITester",
    "text": "Test message from API",
    "timestamp": "2025-11-20T..."
  }
}
```

---

### 6️⃣ Ověření Consumer logů

**Akce:**
1. V Aspire Dashboard přejděte na **Logs**
2. V dropdown vyberte **consumer**
3. Sledujte logy

**Očekávaný výsledek:**
```
✅ Vidíte log: "Starting RabbitMQ Consumer..."
✅ Vidíte log: "Consumer is now listening for messages..."
✅ Po odeslání zprávy (z STOMP nebo API):
   "Received message: {\"Id\":\"...\",\"User\":\"Tester1\",\"Text\":\"Hello from STOMP!\",\"Timestamp\":\"...\"}"
```

---

### 7️⃣ Kontrola zpráv v RabbitMQ

**Akce:**
1. Otevřete RabbitMQ Management (port 15672)
2. Přejděte na **Queues** → klikněte na **chat.queue**
3. Scroll dolů na sekci **"Get messages"**
4. Nastavte:
   - Messages: 10
   - Ack mode: Automatic ack
5. Klikněte **"Get Message(s)"**

**Očekávaný výsledek:**
```
✅ Vidíte seznam zpráv (pokud nějaké čekají ve frontě)
✅ Zprávy mají správný JSON formát
✅ Routing key: chat.message
```

---

## 🎯 Komplexní end-to-end test

**Tento test ověří celý flow:**

1. **Otevřete 3 okna prohlížeče** s STOMP Chat
2. **Připojte všechny 3 klienty** (Tester1, Tester2, Tester3)
3. **Odešlete zprávu z Tester1**

**Očekávaný výsledek:**
```
✅ Zpráva se zobrazí ve VŠECH 3 oknech
✅ V Consumer logu se objeví log o přijaté zprávě
✅ V RabbitMQ Management: Message rates graf ukazuje aktivitu
```

4. **Pošlete zprávu přes API** (PowerShell nebo test-api.http)

**Očekávaný výsledek:**
```
✅ API vrátí success response
✅ Zpráva se zobrazí ve VŠECH 3 STOMP klientech
✅ Consumer log ukáže přijatou zprávu
```

5. **Odpojte Tester2** (klikněte Disconnect)
6. **Pošlete zprávu z Tester1**

**Očekávaný výsledek:**
```
✅ Zpráva se zobrazí u Tester1 a Tester3
❌ Zpráva se NEzobrazí u Tester2 (je odpojený)
✅ Consumer stále přijímá zprávu (nezávisle na STOMP)
```

---

## 🐛 Řešení problémů

### Problém: STOMP se nepřipojí

**Debug kroky:**
1. F12 → Console v prohlížeči
2. Hledejte WebSocket chyby
3. Zkontrolujte port: `ws://localhost:15674/ws`
4. V Aspire Dashboard → rabbitmq → Endpoints - ověřte port 15674

### Problém: Consumer nepřijímá zprávy

**Debug kroky:**
1. Aspire Dashboard → Logs → consumer
2. Hledejte: "Consumer is now listening for messages..."
3. RabbitMQ Management → Queues → chat.queue → zkontrolujte Consumers count (mělo by být 1)

### Problém: API endpoint vrací 500

**Debug kroky:**
1. Aspire Dashboard → Logs → apiservice
2. Zkontrolujte stack trace
3. Ověřte připojení k RabbitMQ (mělo by být v ConnectionStrings)

---

## 📊 Metriky úspěchu

Po úspěšném testu byste měli vidět:

```
✅ Všechny 4 služby Running v Aspire Dashboard
✅ chat.queue má alespoň 1 Consumer v RabbitMQ
✅ STOMP klienti se připojují a odpojují bez chyb
✅ Zprávy se doručují real-time všem klientům
✅ Consumer loguje každou přijatou zprávu
✅ API endpoint vrací success responses
```

---

## 🚀 Quick Test Script

Nejrychlejší test - zkopírujte do PowerShell:

```powershell
Write-Host "🧪 Quick RabbitMQ STOMP Test" -ForegroundColor Cyan

# 1. Test API (změňte port podle vašeho endpointu)
$apiUrl = "https://localhost:7123"  # <-- ZMĚŇTE!
$body = '{"user":"QuickTest","text":"Hello!"}' 

try {
    $result = Invoke-RestMethod -Uri "$apiUrl/messages" -Method Post -Body $body -ContentType "application/json" -SkipCertificateCheck
    Write-Host "✅ API Test PASSED" -ForegroundColor Green
    $result | ConvertTo-Json
} catch {
    Write-Host "❌ API Test FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "👉 Nyní otevřete STOMP Chat v prohlížeči a zkontrolujte, že se zpráva zobrazila!" -ForegroundColor Yellow
```

---

**Hotovo! Aplikace je plně funkční a testovatelná. 🎉**
