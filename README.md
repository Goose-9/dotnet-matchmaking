# Matchmaking Service

A lightweight **ASP.NET Core Web API** that implements a simple matchmaking service.  
Clients can enqueue players into a matchmaking queue, remove them again, and retrieve matches created by the server using a pluggable matchmaking strategy.

The project focuses on clean backend structure, concurrency-safe queue handling, and separation of concerns between API, domain models, and matchmaking logic.

---

## Project Status

### Active Learning & Development Project

This project is currently in an early-to-mid development stage and is being built incrementally with a strong focus on backend fundamentals and deployment practices.

### What’s Implemented

- Core matchmaking service architecture
- Strategy-based matchmaking engine
- REST API with Swagger documentation
- Health check endpoints
- Clean service composition via dependency injection

### What’s In Progress / Planned

- Metrics endpoint for observability
- Unit and integration testing
- Docker containerization
- CI/CD pipeline setup
- Cloud deployment (Azure)
- Minimal frontend client for manual testing

### What This Is (and Isn’t)

- ✅ A production-style backend service built to practice real-world patterns

- ✅ A platform for learning observability, testing, and deployment

- ❌ Not a finished commercial product

- ❌ Not optimized for scale or performance yet

Design decisions favor clarity, correctness, and extensibility over premature optimization.

---

## Why This Project Exists

This repository serves as a hands-on learning project to practice building and deploying a real backend service.
The focus is on:

- Designing a maintainable service architecture
- Adding observability (health checks & metrics)
- Writing automated tests
- Containerizing the service
- Building a CI/CD pipeline
- Deploying the service to a cloud environment

---

## Key Concepts

- RESTful Web API built with ASP.NET Core
- In-memory matchmaking queue
- Strategy-based matchmaking engine
- Thread-safe queue handling
- Simple, testable domain models

---

## Tech Stack

- **.NET** / **C#**
- **ASP.NET Core Web API**
- Dependency Injection
- Concurrent collections (`ConcurrentQueue`, `ConcurrentDictionary`)
- Swagger / OpenAPI for API exploration

---

## Project Structure

``` swift
dotnet-matchmaking/
│
├── Controllers/
│ └── MatchmakingController.cs # HTTP endpoints
│
├── Services/
│ ├── MatchmakingEngine.cs # Core matchmaking logic
│ ├── IMatchmakingStrategy.cs # Strategy abstraction
│ └── Strategies/
│ └── FifoQueueStrategy.cs # FIFO matchmaking implementation
│
├── Models/
│ ├── EnqueueRequest.cs # API request DTO
│ ├── PlayerTicket.cs # Queue representation
│ └── Match.cs # Match domain model
│
├── Program.cs # App bootstrap + DI
├── MatchmakingService.http # Example HTTP requests
├── appsettings.json
└── appsettings.Development.json
```

---

## Architecture Overview

### API Layer

The `MatchmakingController` exposes three endpoints:

- **POST** `/matchmaking/join`  
  Adds a player to the matchmaking queue and returns a unique ticket ID.

- **POST** `/matchmaking/leave`  
  Removes a player from the queue using their ticket ID.

- **GET** `/matchmaking/match` 
  Using their ticket ID, returns the match details containing the player.

Controllers are intentionally thin and delegate all logic to the matchmaking engine.

---

### Matchmaking Engine

`MatchmakingEngine` is responsible for:
- Managing the matchmaking queue
- Creating matches when enough players are available
- Storing created matches in memory

It is designed to work with **any matchmaking strategy** implementing `IMatchmakingStrategy`.

This allows the matching logic to be swapped or extended without changing the API layer.

---

### Matchmaking Strategy

The current implementation uses a **FIFO (First-In, First-Out)** strategy:

- Players are matched strictly in the order they join the queue
- Matches are created in pairs (Player A vs Player B)
- Matching is concurrency-safe using `ConcurrentQueue`

This strategy is implemented in:

``` 
Services/Strategies/FifoQueueStrategy.cs
```

---

### Domain Models

- **PlayerTicket**  
  Represents a queued player, including:
  - Player ID
  - *Elo (TODO)* 
  - *Region (TODO)*
  - Enqueue timestamp (UTC)

- **Match**  
  Represents a completed match between two players, including:
  - Match ID
  - Player A / Player B
  - Creation timestamp (UTC)

---

## Getting Started

### Prerequisites

- .NET SDK (recent LTS recommended)

### Run the API

```bash
dotnet restore
dotnet run
``` 
The service will start on a local port (typically http://localhost:5000 or https://localhost:5001).

### API Documentation (Swagger)

Once the service is running, open:

```bash
/swagger
``` 
Example:
```bash
http://localhost:5000/swagger
``` 

This provides an interactive UI to explore and test the API endpoints.

## Health Checks

The service exposes health check endpoints for monitoring.

Endpoints:
```
/health/live
/health/ready
```

These can be used by:

- Load balancers
- Container orchestrators
- Monitoring systems