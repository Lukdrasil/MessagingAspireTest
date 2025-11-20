# RabbitMQ STOMP Configuration Examples

## Základní STOMP frame struktura

### CONNECT
```
CONNECT
accept-version:1.0,1.1,1.2
heart-beat:0,0
login:guest
passcode:guest

^@
```

### SUBSCRIBE
```
SUBSCRIBE
id:sub-0
destination:/exchange/chat.exchange/chat.#
ack:auto

^@
```

### SEND
```
SEND
destination:/exchange/chat.exchange/chat.message
content-type:application/json
content-length:123

{"User":"Jan","Text":"Hello","Timestamp":"2025-11-20T13:00:00Z"}^@
```

### DISCONNECT
```
DISCONNECT

^@
```

## Destination patterns

### Exchange
```
/exchange/<exchange-name>/<routing-key>
```

Příklady:
- `/exchange/chat.exchange/chat.message` - odeslat do exchange
- `/exchange/chat.exchange/chat.#` - subscribe na všechny zprávy s routing key začínajícím "chat."

### Queue
```
/queue/<queue-name>
```

Příklad:
- `/queue/chat.queue` - přímo ze specifické fronty

### Topic (temporary queue)
```
/topic/<routing-key>
```

Příklad:
- `/topic/chat.broadcast` - dočasná fronta pro broadcast

## ACK modes

### Auto ACK (výchozí)
```
SUBSCRIBE
id:sub-0
destination:/queue/chat.queue
ack:auto

^@
```

### Client ACK
```
SUBSCRIBE
id:sub-0
destination:/queue/chat.queue
ack:client

^@
```

Pak je třeba každou zprávu potvrdit:
```
ACK
id:sub-0
message-id:<message-id-from-MESSAGE-frame>

^@
```

### Client-Individual ACK
```
SUBSCRIBE
id:sub-0
destination:/queue/chat.queue
ack:client-individual

^@
```

## Heartbeat

### Povolení heartbeat
```
CONNECT
accept-version:1.2
heart-beat:10000,10000

^@
```

- První číslo: kolik milisekund může klient poslat heartbeat
- Druhé číslo: kolik milisekund očekává heartbeat od serveru

## Headers a metadata

### Custom headers při SEND
```
SEND
destination:/exchange/chat.exchange/chat.message
content-type:application/json
priority:5
persistent:true
correlation-id:123456
reply-to:/queue/reply.queue

{"User":"Jan","Text":"Hello"}^@
```

### Message properties
- `content-type` - MIME typ zprávy
- `content-length` - délka body
- `persistent` - trvalost zprávy (true/false)
- `priority` - priorita zprávy (0-9)
- `correlation-id` - ID pro korelaci request/response
- `reply-to` - kam poslat odpověď
- `expiration` - expirace zprávy v ms
- `message-id` - unikátní ID zprávy

## JavaScript STOMP knihovny

### Varianta 1: Vanilla WebSocket (naše implementace)
```javascript
const ws = new WebSocket('ws://localhost:15674/ws');
ws.onopen = () => ws.send('CONNECT\naccept-version:1.2\n\n\x00');
```

### Varianta 2: stomp.js / stompjs
```javascript
npm install @stomp/stompjs

import { Client } from '@stomp/stompjs';

const client = new Client({
    brokerURL: 'ws://localhost:15674/ws',
    connectHeaders: {
        login: 'guest',
        passcode: 'guest',
    },
    onConnect: () => {
        client.subscribe('/exchange/chat.exchange/chat.#', message => {
            console.log(JSON.parse(message.body));
        });
    }
});

client.activate();
```

### Varianta 3: SockJS + STOMP
```javascript
npm install sockjs-client @stomp/stompjs

import SockJS from 'sockjs-client';
import { Stomp } from '@stomp/stompjs';

const socket = new SockJS('http://localhost:15674/stomp');
const stompClient = Stomp.over(socket);

stompClient.connect({}, frame => {
    stompClient.subscribe('/exchange/chat.exchange/chat.#', message => {
        console.log(JSON.parse(message.body));
    });
});
```

## RabbitMQ Configuration

### Povolení pluginů (Docker)
```dockerfile
FROM rabbitmq:3-management

RUN rabbitmq-plugins enable rabbitmq_stomp rabbitmq_web_stomp
```

### Nebo pomocí rabbitmq.conf
```conf
# STOMP plugin configuration
stomp.default_user = guest
stomp.default_pass = guest
stomp.tcp_listeners.1 = 61613

# Web STOMP plugin
web_stomp.tcp.port = 15674
web_stomp.ws_frame = binary
web_stomp.cowboy_opts.max_frame_size = 65536
```

### Environment variables (Aspire/Docker)
```csharp
.WithEnvironment("RABBITMQ_PLUGINS", "rabbitmq_management rabbitmq_stomp rabbitmq_web_stomp")
.WithEnvironment("RABBITMQ_STOMP_TCP_PORT", "61613")
.WithEnvironment("RABBITMQ_WEB_STOMP_TCP_PORT", "15674")
```

## Debugging STOMP

### Chrome/Edge DevTools
1. Otevřete DevTools (F12)
2. Network tab
3. Filtrujte "WS" (WebSocket)
4. Klikněte na spojení
5. Messages tab - vidíte všechny STOMP frames

### RabbitMQ Management UI
1. Otevřete http://localhost:15672
2. Connections - vidíte STOMP připojení
3. Channels - vidíte aktivní kanály
4. Queues - vidíte zprávy ve frontách

### Logs
Aspire Dashboard → Logs → rabbitmq

## Bezpečnost

### SSL/TLS pro Web STOMP
```javascript
const ws = new WebSocket('wss://your-domain.com:15673/ws');
```

RabbitMQ konfigurace:
```conf
web_stomp.ssl.port = 15673
web_stomp.ssl.certfile = /path/to/cert.pem
web_stomp.ssl.keyfile = /path/to/key.pem
```

### Autentizace
```
CONNECT
accept-version:1.2
login:myuser
passcode:mypassword

^@
```

### Virtual Hosts
```
CONNECT
accept-version:1.2
host:/myvhost

^@
```

Nebo v destination:
```
/exchange/<vhost>/<exchange>/<routing-key>
```

## Performance tips

1. **Použijte binary frames** místo text pro Web STOMP
2. **Batch zprávy** - pošlete více zpráv najednou
3. **Heartbeat** nastavte rozumně (ne příliš často)
4. **Connection pooling** - znovu použijte spojení
5. **Prefetch** - nastavte vhodný prefetch count pro consumers
6. **Persistent connections** - minimalizujte reconnects

## Další resources

- [RabbitMQ STOMP Plugin Documentation](https://www.rabbitmq.com/stomp.html)
- [STOMP Protocol Specification](https://stomp.github.io/stomp-specification-1.2.html)
- [RabbitMQ Web STOMP Plugin](https://www.rabbitmq.com/web-stomp.html)
- [Aspire RabbitMQ Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/messaging/rabbitmq-client-component)
