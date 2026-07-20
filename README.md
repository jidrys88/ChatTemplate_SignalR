# 💬 .NET 10 & C# 13 SignalR Clean Architecture Chat Template

Eine enterprise-ready, hochperformante und modulare Projekt-Schablone für ein Echtzeit-Chat-System basierend auf **.NET 10**, **C# 13** und **SignalR**. Die Architektur folgt strikt den Prinzipien der **Clean Architecture** und ist als wiederverwendbares `dotnet new`-Template konfiguriert.

---

## 📋 Inhaltsverzeichnis
- [🚀 Key Features & Vorteile](#-key-features--vorteile)
- [🏗️ Architektur & Projektstruktur](#️-architektur--projektstruktur)
- [🛠️ Technische Highlights (C# 13 & .NET 10)](#️-technische-highlights-c-13--net-10)
- [🔌 API & SignalR Hub Spezifikation](#-api--signalr-hub-spezifikation)
- [⚙️ Konfiguration (`appsettings.json`)](#️-konfiguration-appsettingsjson)
- [🚀 Schnellstart & Template-Installation](#-schnellstart--template-installation)
- [📝 Lizenz](#-lizenz)

---

## 🚀 Key Features & Vorteile

* **Clean Architecture**: Strikte Entkopplung von Domain-Logik, Datenhaltung und API-Schicht. Keine externen Abhängigkeiten im Core.
* **Zukunftssicher (.NET 10 & C# 13)**: Verwendung modernster C# 13 Features wie `System.Threading.Lock`, Primary Constructors und Collection Expressions.
* **Stark typisierter SignalR Hub**: Maximale Typsicherheit über `Hub<IChatClient>` – verhindert Tippfehler bei Event-Namen zwischen Server und Client.
* **Effizientes Streaming**: Lade Chat-Historien speicherschonend mittels `IAsyncEnumerable<ChatMessageDto>` statt großer RAM-Arrays.
* **Flexible Datenhaltung**: Nahtloser Wechsel per Konfiguration zwischen **In-Memory** (ideal für Prototypen/Tests) und **EF Core** (PostgreSQL/SQLite).
* **Enterprise Error Handling**: Ein globaler `IHubFilter` fängt `ChatDomainException` ab und leitet sie sicher an den Client weiter, während interne Systemfehler verborgen bleiben.
* **JWT-Authentication**: Vollständiges Token-Handling inklusive automatischer Extraktion aus Query-Strings (`?access_token=...`) für WebSockets.
* **Skalierbar**: Vorbereitet für SignalR Backplanes (Redis / Azure SignalR Service) bei horizontaler Skalierung.
* **Developer Experience**: Fertiger C# Console-Client und integrierte Browser-UI zum sofortigen Testen.

---

## 🏗️ Architektur & Projektstruktur

Das Solution-Layout ist nach den Prinzipien der **Clean Architecture** in vier entkoppelte Projekte aufgeteilt:

```text
ChatTemplate/
├── .template.config/
│   └── template.json                 # dotnet new CLI Template Configuration
├── ChatTemplate.sln                  # Multi-Project Solution
└── src/
    ├── ChatTemplate.Core/            # 🎯 Pure Domain-Logik, Entities, DTOs & Interfaces (Keine Abhängigkeiten)
    │   ├── DTOs/
    │   │   ├── ChatMessageDto.cs
    │   │   └── UserPresenceDto.cs
    │   ├── Entities/
    │   │   └── ChatMessage.cs
    │   ├── Exceptions/
    │   │   └── ChatDomainException.cs # Business Fault Exceptions
    │   └── Interfaces/
    │       ├── IChatClient.cs        # Strongly-typed SignalR Client Methods
    │       ├── IChatRepository.cs
    │       └── IPresenceTracker.cs
    │
    ├── ChatTemplate.Infrastructure/  # ⚙️ Data Access, Persistence & State Management
    │   ├── Persistence/
    │   │   ├── ChatDbContext.cs      # EF Core DbContext (PostgreSQL / SQLite)
    │   │   ├── EfChatRepository.cs   # Relational DB Persistence
    │   │   └── InMemoryChatRepository.cs # High-performance In-Memory Fallback
    │   └── Presence/
    │       └── PresenceTracker.cs    # Multi-Device Connection Tracker (C# 13 Lock)
    │
    ├── ChatTemplate.API/             # 🌐 Web API Host, SignalR Hub & Middleware
    │   ├── Controllers/
    │   │   └── AuthController.cs     # JWT Token Endpoint
    │   ├── Filters/
    │   │   └── SignalRErrorHandlingFilter.cs # Global IHubFilter (Domain vs System Errors)
    │   ├── Hubs/
    │   │   └── ChatHub.cs            # Strongly-typed SignalR Hub
    │   ├── Providers/
    │   │   └── CustomUserIdProvider.cs # Claims-to-SignalR User ID Mapping
    │   └── wwwroot/
    │       └── index.html            # Native JS Browser Chat Client
    │
    └── ChatTemplate.Client/          # 🖥️ Interactive C# Console Client
        └── Program.cs                # HubConnection mit Automatic Reconnect
```

---

## 🛠️ Technische Highlights (C# 13 & .NET 10)

* **C# 13 `System.Threading.Lock`**: Im `PresenceTracker` kommt das neue, hochperformante `Lock`-Objekt aus C# 13 für thread-sicheres Multi-Geräte-Tracking zum Einsatz.
* **Clean Boundaries**: `ChatTemplate.Core` hat **0 external dependencies**. Sämtliche Infrastruktur-Details (EF Core, SignalR) sind in äußeren Schichten gekapselt.
* **Error Sanitization**: `SignalRErrorHandlingFilter` fängt `ChatDomainException` ab und sendet verständliche Fachfehler an den Client. Systemcrashes werden mit `TraceId` geloggt und sicher maskiert.
* **Streaming History**: `GetMessageHistoryStreamAsync` nutzt `IAsyncEnumerable<ChatMessageDto>`, um große Chat-Historien speicherschonend in Chunks an Clients zu streamen.

---

## 🔌 API & SignalR Hub Spezifikation

### HTTP Endpoints (`AuthController`)

| Methode | Endpoint | Beschreibung | Request Body | Response |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Generiert ein Test-JWT | `{ "userId": "string", "userName": "string" }` | `{ "token": "string", "userId": "string", "userName": "string" }` |

### SignalR Hub Methods (`/hubs/chat`)

Authentifizierung erforderlich via JWT (`Authorization: Bearer <token>` oder Query String `?access_token=<token>`).

#### Server-Methoden (Client ➔ Server)

```csharp
Task JoinRoom(string roomName)
Task LeaveRoom(string roomName)
Task SendGroupMessage(string roomName, string content)
Task SendPrivateMessage(string recipientUserId, string content)
IAsyncEnumerable<ChatMessageDto> GetMessageHistoryStreamAsync(string targetId, CancellationToken cancellationToken)
```

#### Client-Events (`IChatClient` Interfaces: Server ➔ Client)

```csharp
Task ReceiveMessage(ChatMessageDto message)
Task ReceiveSystemNotification(string notification)
Task UserPresenceChanged(string userId, bool isOnline)
Task UserJoinedRoom(string roomName, string userId)
Task UserLeftRoom(string roomName, string userId)
```

---

## ⚙️ Konfiguration (`appsettings.json`)

Die Anwendung kann ohne Code-Änderungen an verschiedene Umgebungen angepasst werden:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.SignalR": "Debug"
    }
  },
  "JwtSettings": {
    "Secret": "SuperSecretKeyForJWTTokenGeneration_MustBeAtLeast32BytesLong!",
    "Issuer": "ChatTemplateAPI",
    "Audience": "ChatTemplateClients",
    "ExpirationInMinutes": 120
  },
  "DatabaseSettings": {
    "UsePersistentDatabase": false, // 'false' = InMemory, 'true' = EF Core (PostgreSQL/SQLite)
    "Provider": "SQLite",           // Optionen: "SQLite", "PostgreSQL"
    "ConnectionString": "Data Source=chat.db"
  },
  "SignalR": {
    "BackplaneType": "None",       // Optionen: "None", "Redis", "AzureSignalR"
    "RedisConnectionString": "localhost:6379",
    "AzureSignalRConnectionString": ""
  }
}
```

---

## 🚀 Schnellstart & Template-Installation

### 1. Template lokal installieren

Klone das Repository oder entpacke den Quellcode und installiere das Template in der `dotnet` CLI:

```bash
dotnet new install .
```

### 2. Neues Projekt generieren

Erstelle eine neue Chat-Solution mit deinem Wunschnamen:

```bash
dotnet new signalr-chat -n MyCompany.ChatApp
```

### 3. API & Clients ausführen

**Web API Host starten:**
```bash
cd src/ChatTemplate.API
dotnet run
```
- **Web UI**: `http://localhost:5000`
- **Swagger / Open API Docs**: `http://localhost:5000/swagger`

**C# Console Client starten:**
```bash
cd src/ChatTemplate.Client
dotnet run
```

---

## 📝 Lizenz

Dieses Projekt ist unter der **MIT-Lizenz** lizenziert.
