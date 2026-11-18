<div align="center">

# 🎟️ SeatSync
### Real-Time Event Seat Booking & Concurrency Control Engine

[![.NET](https://img.shields.io/badge/.NET-ASP.NET%20Core%20Web%20API-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![SignalR](https://img.shields.io/badge/Real--Time-SignalR%20WebSockets-0078D4?style=for-the-badge&logo=microsoft&logoColor=white)](https://learn.microsoft.com/aspnet/core/signalr/)
[![React](https://img.shields.io/badge/Frontend-React%20%2B%20Vite-61DAFB?style=for-the-badge&logo=react&logoColor=black)](https://react.dev/)
[![SQLite](https://img.shields.io/badge/Database-SQLite%20%2B%20EF%20Core-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![xUnit](https://img.shields.io/badge/Testing-xUnit%20Concurrency%20Tests-2EA44F?style=for-the-badge)](https://xunit.net/)

*High-concurrency stadium & event ticket booking system designed to eliminate double-booking race conditions through real-time WebSocket broadcasts, atomic seat locking, and idempotent wallet transactions.*

</div>

---

## ⚡ Why SeatSync?

In high-demand ticket sales (such as IPL finals or concerts), multiple users frequently click the exact same seat within milliseconds. Without strict concurrency control, standard APIs either overwrite bookings or charge multiple users for one seat.

**SeatSync** solves this end-to-end:
* **Instant Visual Sync:** When User A selects a seat, every connected browser immediately sees that seat turn **Locked (Yellow)** via **ASP.NET Core SignalR**—no page refresh required.
* **Race-Condition Guard (`409 Conflict`):** If User B sends a simultaneous lock or booking request for the same seat, the backend rejects the collision with an explicit `409 Conflict` response.
* **Idempotent Wallet Payments:** Confirming a booking atomically deducts ticket cost from the user's wallet, records a `WalletTransaction`, transitions the seat to **Booked (Red)** across all clients, and prevents duplicate charges on network retries.
* **Automated Concurrency Verification:** Backed by **xUnit** integration tests simulating parallel multi-user booking requests against the exact same seat.

---

## 🏗️ System Architecture

```text
┌─────────────────────────┐         HTTP REST (JSON)         ┌─────────────────────────────┐
│   React + Vite Client   │ ───────────────────────────────► │   ASP.NET Core Web API      │
│  (Live Stadium Grid)    │ ◄─────────────────────────────── │  (Controllers & Services)   │
└───────────┬─────────────┘      409 Conflict / 200 OK       └──────────────┬──────────────┘
            │                                                               │
            │                 SignalR Hub (/hubs/seats)                     │ EF Core
            └─────────────── Real-Time Seat State Broadcast ────────────────┤
                                                                            ▼
                                                             ┌─────────────────────────────┐
                                                             │       SQLite Database       │
                                                             │  Events • Seats • Bookings  │
                                                             │    Users • Wallets • Txns   │
                                                             └─────────────────────────────┘
[![SeatSync CI](https://github.com/afsalcricket-a11y/SeatSync/actions/workflows/ci.yml/badge.svg)](https://github.com/afsalcricket-a11y/SeatSync/actions/workflows/ci.yml)
