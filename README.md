# 💳 Payment System

A .NET 10 payment processing simulation that models the communication between an ATM, a payment gateway (GT), and a mock card issuer. Built with a custom binary TCP protocol, real-time SignalR event streaming, and a clean layered architecture.

---

> ⚠️ Startup Order — Critical
>
> Always start Payment.GT before Payment.API.
> The API opens a persistent TCP connection to the GT on the first incoming request.
> If the GT is not already listening, the connection will be actively refused and all requests will fail.

---

## 📁 Solution Structure

    Payment.System/
    ├── Payment.GT/                  # TCP gateway server — standalone Generic Host
    ├── Payment.API/                 # ASP.NET Core 10 REST API
    ├── Payment.API.DataServices/    # GT client, connection layer, business services
    ├── Payment.Protocol/            # Binary frame codec and TLV mapper
    ├── Payment.Shared/              # Shared DTOs and typed configuration options
    └── Payment.Hubs/                # SignalR hub for real-time ATM event streaming

---

## 🏗️ Architecture Overview

                        HTTP
    ATM Client  ──────────────────►  Payment.API
                                           │
                                  TCP (port 5555)
                                           │
                                           ▼
                                      Payment.GT
                                           │
                                   in-process call
                                           │
                                           ▼
                                  MockIssuerClient
                              (simulated card issuer)

---

## 🔧 Components

### Payment.GT — Gateway TCP Server

A Generic Host console application that simulates a payment gateway. It accepts raw TCP connections, processes binary payment frames, and delegates authorisation decisions to the mock issuer.

Key classes:

| Class | Responsibility |
|---|---|
| TcpServerHostedService | BackgroundService that starts TcpListener and accepts client connections in a loop |
| ConnectionHandler | Reads/writes frames on a single accepted TcpClient |
| GatewayProcessor | Routes frames to the correct handler by message type |
| MockIssuerClient | Simulated card issuer with hard-coded decision rules |

Configuration — appsettings.json:

    {
      "TcpPort": 5555
    }

---

### Payment.API — REST API

An ASP.NET Core 10 web application that exposes HTTP endpoints for ATM operations and communicates with the GT exclusively over TCP.

Endpoints:

| Method | Route | Description |
|---|---|---|
| POST | /api/withdrawals/reserve | Reserve funds for a withdrawal |

Key classes:

| Class | Responsibility |
|---|---|
| WithdrawalsController | Thin HTTP controller, delegates to service layer |
| WithdrawalsService | Generates STAN and correlation ID, builds request, maps response |
| ExceptionHandlingMiddleware | Translates unhandled exceptions to RFC 7807 ProblemDetails |
| AtmHub | SignalR hub; broadcasts payment lifecycle events to connected clients |

Configuration — appsettings.json:

    {
      "GatewayClient": {
        "Host": "localhost",
        "Port": 5555,
        "TimeoutMs": 2000,
        "MaxRetries": 2,
        "HeartbeatSeconds": 15,
        "HeartbeatTimeoutMs": 2000
      }
    }

---

### Payment.API.DataServices — GT Client Layer

Owns all TCP connection management and the request/response correlation mechanism.

Key classes:

| Class | Responsibility |
|---|---|
| GtConnection | Manages TcpClient, reads frames via System.IO.Pipelines, heartbeat Ping/Pong, auto-reconnect |
| GtClient | Correlates outbound requests with inbound responses via ConcurrentDictionary keyed by CorrelationId, retry and timeout |

Request flow:

    WithdrawalsService
      └─► GtClient.SendAndWaitWithRetryAsync()
              ├─► GtConnection.EnsureConnectedAsync()    ← TCP connect / reconnect
              ├─► GtConnection.SendAsync()               ← write binary frame
              └─► await TaskCompletionSource<Frame>       ← suspended until GT responds
                      └─ resolved by ReadLoopAsync()
                         when the matching CorrelationId arrives

---

### Payment.Protocol — Binary Protocol Codec

Stateless library shared by both Payment.GT and Payment.API.DataServices.

| Class | Responsibility |
|---|---|
| FrameOperator | Serialises and deserialises Frame objects to/from length-prefixed binary |
| TlvMapper | Maps between strongly-typed DTOs and raw TLV lists via reflection |
| ObjectCreator | Combines TlvMapper and FrameOperator to produce wire-ready byte arrays |

---

### Payment.Shared — Shared Models

- GtClientOptions — Typed options class for configuration binding.
- All request/response DTOs shared across projects: ReserveWithdrawalRequest, ReserveWithdrawalResponse, IssuerDecision, etc.

---

### Payment.Hubs — SignalR

- AtmHub — Broadcasts real-time payment lifecycle events keyed by ATM ID. Connect at /hubs/atm.

---

## 🚀 Getting Started

### Prerequisites

- .NET 10 SDK  (https://dotnet.microsoft.com/download)
- Visual Studio 2026 or the dotnet CLI

---

### Option A — Visual Studio (Recommended)

1. Right-click the Solution → Set Startup Projects…
2. Choose Multiple startup projects.
3. Set Payment.GT → Start and Payment.API → Start.
4. Drag Payment.GT above Payment.API so it starts first.
5. Press F5.

Wait for this log line from the GT before sending any requests:

    [HH:mm:ss INF] GT Gateway listening on port 5555

---

### Option B — CLI (Two Terminals)

Terminal 1 — GT first:

    cd Payment.GT
    dotnet run

Wait until you see:

    [HH:mm:ss INF] GT Gateway listening on port 5555

Terminal 2 — API second:

    cd Payment.API
    dotnet run

---

## 🧪 Test PANs

The MockIssuerClient inside Payment.GT uses the following hard-coded rules:

| PAN | Rule | RC | Outcome |
|---|---|---|---|
| 4111111111111111 | Exact match | 00 | Approved — auth code 831992 |
| Starts with 5 | Prefix check | 51 | Declined — Insufficient Funds |
| Ends with 0000 | Suffix check | 00 | Approved after 5-second delay — use to test timeout/retry behaviour |
| Anything else | Default | 05 | Declined — Do Not Honor |

---

## 🔌 Protocol Reference

All messages between the API and GT are length-prefixed binary frames containing TLV-encoded payloads, sent over a persistent TCP connection.

Frame structure:

    ┌────────────────────────────────────────────────┐
    │  Frame                                          │
    │  ┌──────────┬──────────┬──────────────────────┐ │
    │  │ Version  │ MsgType  │  TLV payload ...     │ │
    │  └──────────┴──────────┴──────────────────────┘ │
    └────────────────────────────────────────────────┘

Message types:

| Type | Direction | Description |
|---|---|---|
| A70 | API → GT | Reserve withdrawal request |
| A71 | GT → API | Reserve withdrawal response |
| A72 | API → GT | Confirm / cancel withdrawal request |
| A73 | GT → API | Confirm / cancel withdrawal response |
| Ping | API → GT | Heartbeat probe |
| Pong | GT → API | Heartbeat acknowledgement |

---

## 📡 Real-Time Events (SignalR)

Connect to /hubs/atm to receive live events for each request lifecycle stage.

| Event | Payload | Fired when |
|---|---|---|
| ReserveRequest | atmId, stan, correlationId, rc, authCode, message | On request sent and on response received |

---

## ⚠️ Known Limitations

- The PIN block is a toy implementation for demonstration purposes and must not be used in production.
- The GT is a single-tenant, in-process simulator. It does not connect to a real card network.
- There is no authentication or TLS on the TCP channel between the API and the GT.
- Mock issuer decisions are static and cannot be configured at runtime without a code change.