# ATM ↔ GT Protocol Gateway

## Overview

This solution simulates an ATM communicating with a Terminal Gateway
(GT) using a binary TLV protocol over TCP.

The system contains two main applications:

1.  ATM Service (REST → TCP TLV Client)
2.  GT Gateway (TCP TLV Server)

A mock issuer is used to simulate real authorization responses.

------------------------------------------------------------------------

# Architecture

Client → ATM REST API → TCP TLV → GT Gateway → Mock Issuer\
↓\
Reservation State Store

------------------------------------------------------------------------

# Applications

============================================================== 1️⃣ ATM
SERVICE ==============================================================

Responsibilities: - Exposes REST API - Generates CorrelationId and
STAN - Builds TLV messages (A70 / A72) - Sends to GT via TCP - Parses
TLV responses (A71 / A73) - Returns JSON responses

------------------------------------------------------------------------

## REST Endpoints

### POST /api/withdrawals/reserve

Request:

{ "pan": "4111111111111111", "pin": "1234", "expiry": "2501", "amount":
1000, "currency": "EUR", "atmId": "ATM001" }

Response:

{ "rc": "00", "authCode": "831992", "correlationId": "GUID", "stan":
123456 }

------------------------------------------------------------------------

### POST /api/withdrawals/complete

Request:

{ "atmId": "ATM001", "stan": 123456, "correlationId": "GUID", "amount":
1000 }

Response:

{ "rc": "00", "completionStatus": "Full" }

------------------------------------------------------------------------

Key Features: - Structured logging (CorrelationId, STAN, ATM ID) - TCP
abstraction - TLV serialization/deserialization - Timeout handling -
Idempotent-safe retry behavior

============================================================== 2️⃣ GT
GATEWAY ==============================================================

Responsibilities: - Accept TCP connections - Parse TLV frames - Validate
DTOs - Handle Reservation (A70 → A71) - Handle Completion (A72 → A73) -
Maintain in-memory state - Enforce idempotency rules - Call issuer -
Serialize response frames

------------------------------------------------------------------------

## TLV Message Types

0x70 → A70 (Reserve)\
0x71 → A71 (Reserve Response)\
0x72 → A72 (Complete)\
0x73 → A73 (Completion Response)

------------------------------------------------------------------------

## Reservation Flow

1.  Validate request
2.  Check idempotency (atmId + stan)
3.  Call issuer
4.  Store reservation
5.  Return A71 response

------------------------------------------------------------------------

## Completion Flow

1.  Validate request
2.  Check reservation exists
3.  Validate completion amount
4.  Return:
    -   Full completion
    -   Partial completion
    -   Failed completion
5.  Cache serialized response for retries

------------------------------------------------------------------------

## Idempotency

Based on: (atmId, stan)

Fingerprint is used to detect mismatched repeated requests.

Repeated identical requests return cached response. Mismatched payload
with same STAN returns error.

============================================================== 3️⃣ MOCK
ISSUER ==============================================================

MockIssuerClient simulates issuer behavior.

Authorization Rules:

PAN = 4111111111111111 → Approved (RC = 00)

PAN starts with 5 → Insufficient funds (RC = 51)

PAN ends with 0000 → Simulated 5 second delay

Otherwise → Do not honor (RC = 05)

Example Approved Response:

{ "rc": "00", "authCode": "831992", "message": "APPROVED" }

============================================================== STATE
MANAGEMENT
==============================================================

In-memory state:

(atmId, stan) → ReservationState

Stored Data: - Approved amount - RC - AuthCode - Completion status -
Serialized response cache - Fingerprint

============================================================== RUNNING
THE SOLUTION
==============================================================

1️⃣ Start GT Gateway:

dotnet run --project Payment.GT

Default TCP Port: 9000

2️⃣ Start ATM Service:

dotnet run --project AtmService

Default REST Port: 5000 / 5001

============================================================== TESTING
SCENARIOS ==============================================================

Approved Case: PAN: 4111111111111111

Insufficient Funds: PAN: 5111111111111111

Timeout Simulation: PAN: 4111111111110000

============================================================== DESIGN
HIGHLIGHTS
==============================================================

-   Clean layered architecture
-   Binary TLV framing
-   Single-flight concurrency protection
-   Idempotent processing
-   Structured logging with CorrelationId and STAN
-   Professional backend design

============================================================== FUTURE
IMPROVEMENTS
==============================================================

-   Replace in-memory store with Redis
-   Add persistent database layer
-   Add integration tests
-   Add Docker support
-   Add monitoring & metrics

==============================================================

Educational Purpose: This project demonstrates TCP protocol
implementation, financial transaction lifecycle (Reservation →
Completion), and production-grade backend architecture in .NET.
