# RTGame

RTGame is a multiplayer real-time game project developed as a practice project for NPG Soft.

## Architecture

The project uses a client-server architecture:

- **Server**: C# .NET 10.0 application with PostgreSQL database
- **Client**: C++ application with SFML for graphics and ImGui for UI

## Features

- Real-time multiplayer gameplay
- Account system with authentication
- Room-based matchmaking
- World/map system
- Network protocol with TCP and UDP support
- Client-side prediction and interpolation
- Snapshot-based state synchronization

## Tech Stack

**Server:**
- .NET 10.0
- Npgsql (PostgreSQL driver)
- Custom network protocol

**Client:**
- C++
- SFML (graphics, window, network)
- ImGui (UI)
- vcpkg package manager

## Project Structure

```
RTGame/
├── Server/             # C# server application
│   ├── Accounts/       # Account management
│   ├── Database/       # Database layer
│   ├── Maps/           # Game maps
│   ├── Network/        # Network protocol
│   └── World/          # Game world logic
└── Client/             # C++ client application
    ├── ImGui UI
    ├── Network (TCP/UDP)
    ├── Rendering
    └── Game logic
```

## Building

**Server:**
```bash
cd Server
dotnet build
```

**Client:**
```bash
cd Client
vcpkg install
# Build using Visual Studio or MSBuild
```

## Running

1. Set up PostgreSQL database
2. Configure server connection settings
3. Start the server: `cd Server && dotnet run`
4. Start the client and connect to the server

## License

See LICENSE.txt for details.

---

[Русская версия](README.ru.md)
