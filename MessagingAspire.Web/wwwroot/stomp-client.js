// STOMP.js client for RabbitMQ WebSocket communication
window.stompClient = (function () {
    let client = null;
    let dotNetRef = null;
    let currentUser = 'Anonymous';
    let stompLogin = 'guest';
    let stompPasscode = 'guest';
    let stompVHost = '/';

    function connect(dotNetReference, url, userName, login, passcode, vhost) {
        dotNetRef = dotNetReference;
        currentUser = userName || 'Anonymous';
        stompLogin = login || 'guest';
        stompPasscode = passcode || 'guest';
        stompVHost = vhost || '/';

        try {
            // Create WebSocket connection
            const ws = new WebSocket(url);

            ws.onopen = function () {
                console.log('WebSocket connected');
                sendStompConnect(ws);
            };

            ws.onmessage = function (event) {
                console.log('Received:', event.data);
                handleStompFrame(event.data);
            };

            ws.onerror = function (error) {
                console.error('WebSocket error:', error);
                dotNetRef.invokeMethodAsync('OnError', 'WebSocket connection error');
            };

            ws.onclose = function () {
                console.log('WebSocket closed');
                dotNetRef.invokeMethodAsync('OnDisconnected');
                client = null;
            };

            client = ws;
        } catch (error) {
            console.error('Connection error:', error);
            dotNetRef.invokeMethodAsync('OnError', error.message);
        }
    }

    function sendStompConnect(ws) {
        // STOMP CONNECT frame (include credentials & host)
        const connectFrame = 'CONNECT\n' +
            'accept-version:1.2\n' +
            'login:' + stompLogin + '\n' +
            'passcode:' + stompPasscode + '\n' +
            'host:' + stompVHost + '\n' +
            'heart-beat:10000,10000\n' +
            '\n' +
            '\x00';

        ws.send(connectFrame);
        console.log('Sent CONNECT frame with auth');
    }

    function handleStompFrame(data) {
        const lines = data.split('\n');
        const command = lines[0];

        console.log('STOMP command:', command);

        if (command === 'CONNECTED') {
            console.log('STOMP connected');
            subscribeToQueue();
            dotNetRef.invokeMethodAsync('OnConnected');
        } else if (command === 'MESSAGE') {
            handleMessage(data);
        } else if (command === 'ERROR') {
            const errorMessage = lines[lines.length - 2] || 'Unknown error';
            dotNetRef.invokeMethodAsync('OnError', errorMessage);
        }
    }

    function subscribeToQueue() {
        if (!client) return;

        // STOMP SUBSCRIBE frame
        const subscribeFrame = 'SUBSCRIBE\n' +
            'id:sub-0\n' +
            'destination:/exchange/chat.exchange/chat.#\n' +
            'ack:auto\n' +
            '\n' +
            '\x00';

        client.send(subscribeFrame);
        console.log('Subscribed to chat.exchange');
    }

    function handleMessage(frame) {
        // Parse STOMP MESSAGE frame
        const lines = frame.split('\n');
        let bodyStartIndex = 0;

        // Find where headers end (empty line)
        for (let i = 0; i < lines.length; i++) {
            if (lines[i] === '') {
                bodyStartIndex = i + 1;
                break;
            }
        }

        // Get message body (everything after headers)
        const body = lines.slice(bodyStartIndex).join('\n').replace(/\x00$/, '');

        try {
            const message = JSON.parse(body);
            dotNetRef.invokeMethodAsync('OnMessageReceived',
                message.User || 'Unknown',
                message.Text || '',
                message.Timestamp || new Date().toISOString()
            );
        } catch (error) {
            console.error('Failed to parse message:', error);
        }
    }

    function sendMessage(text) {
        if (!client || client.readyState !== WebSocket.OPEN) {
            console.error('Not connected');
            return;
        }

        const message = {
            Id: generateUUID(),
            User: currentUser,
            Text: text,
            Timestamp: new Date().toISOString()
        };

        const body = JSON.stringify(message);

        // STOMP SEND frame
        const sendFrame = 'SEND\n' +
            'destination:/exchange/chat.exchange/chat.message\n' +
            'content-type:application/json\n' +
            'content-length:' + body.length + '\n' +
            '\n' +
            body +
            '\x00';

        client.send(sendFrame);
        console.log('Sent message:', message);
    }

    function disconnect() {
        if (!client) return;

        // STOMP DISCONNECT frame
        const disconnectFrame = 'DISCONNECT\n' +
            '\n' +
            '\x00';

        client.send(disconnectFrame);
        
        setTimeout(() => {
            if (client) {
                client.close();
                client = null;
            }
        }, 100);
    }

    function generateUUID() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    return {
        connect: connect,
        sendMessage: sendMessage,
        disconnect: disconnect
    };
})();
