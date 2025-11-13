# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OrleansEdge is a distributed edge computing system that demonstrates how to use Microsoft Orleans (virtual actor framework) to control IoT hardware on Raspberry Pi devices. It implements a remote LED control system where a controller application sends commands to edge devices (Raspberry Pis) to control their LEDs.

**Key concept:** Brings cloud-native distributed patterns (Orleans virtual actors) to resource-constrained edge devices with state persistence and cluster management.

## Architecture

The system follows a client-server-shared pattern with three projects:

### OrleansEdge.Core (Shared Library)
- Defines contracts used by both controller and node
- **ILedControllerGrain**: Orleans grain interface for LED control (`SetLedColor`, `GetCurrentColor`)
- **LedColor**: Enum defining supported colors (Off, Red, Green, Blue, Yellow, Cyan, Magenta, White)

### OrleansEdge.Node (Edge Device / Orleans Silo)
- Runs on Raspberry Pi as an Orleans silo hosting grain implementations
- **LedControllerGrain**: Grain implementation with persistent state stored in SQLite
- **MeadowApplication/OutputService**: Hardware abstraction layer for Raspberry Pi LED control
- Uses SQLite for Orleans clustering membership and grain state persistence
- Default ports: Silo 11111, Gateway 30000

### OrleansEdge.Controller (Client Application)
- Terminal-based UI using Terminal.Gui framework
- Connects to Orleans cluster via static gateway endpoints
- Sends commands to grain: `color <colorname>`

### Communication Flow
1. Controller connects to Orleans cluster via gateway endpoint
2. Gets grain reference: `GetGrain<ILedControllerGrain>("led")`
3. Grain state automatically persisted to SQLite on changes
4. Grain communicates with hardware via Meadow OutputService
5. State survives grain deactivation and application restarts

## Build Commands

Build the entire solution:
```bash
dotnet build OrleansEdge.slnx
```

Build individual projects:
```bash
dotnet build OrleansEdge.Core/OrleansEdge.Core.csproj
dotnet build OrleansEdge.Node/OrleansEdge.Node.csproj
dotnet build OrleansEdge.Controller/OrleansEdge.Controller.csproj
```

## Running the System

### Development Setup (Local Testing)
Run the node (silo):
```bash
dotnet run --project OrleansEdge.Node/OrleansEdge.Node.csproj
```

Run the controller (client):
```bash
dotnet run --project OrleansEdge.Controller/OrleansEdge.Controller.csproj
```

### Production Setup (Raspberry Pi)
1. Deploy Node to Raspberry Pi with appsettings.Production.json
2. Controller uses appsettings.Production.json with gateway at `192.168.5.3:30000`
3. Primary silo runs at `192.168.4.22:11111`

## Configuration

The system uses appsettings.json files with Development and Production variants:

**Node configuration:**
- `Orleans:SiloPort` - Port for silo-to-silo communication (default: 11111)
- `Orleans:GatewayPort` - Port for client connections (default: 30000)
- `Orleans:PrimarySiloEndpoint` - Primary silo for clustering
- `Orleans:Storage:ConnectionString` - SQLite database path

**Controller configuration:**
- `Orleans:Gateways` - Array of gateway endpoints to connect to

## Key Architectural Patterns

1. **Virtual Actor Pattern**: Each LED is a grain with unique identity; Orleans handles location transparency and routing

2. **Persistent State**: LedState includes CurrentColor, LastUpdated, and ControllingNodeId, stored in SQLite via Orleans ADO.NET provider

3. **Hardware Abstraction**: Meadow platform provides cross-platform IoT hardware support; OutputService abstracts physical LED control

4. **Development Clustering**: Uses `UseDevelopmentClustering()` for simplified cluster setup; production would use ADO.NET clustering

## External Dependencies

**Important**: This solution references Meadow projects from `../../wilderness/` directory:
- Meadow.Contracts
- Meadow.Linux (for Raspberry Pi support)
- Meadow.AspNetCore.Abstractions
- Meadow.Core
- Meadow.Foundation.Core
- Meadow.Units

These dependencies must be available at the relative path for builds to succeed.

## State Management

Grain state is initialized in LedControllerGrain.cs and automatically persisted:
- SQLite database schema created by `InitializeSqliteDatabase()` in Program.cs
- Uses Orleans storage tables: GrainId, ServiceId, PayloadBinary/Json/Xml, Version, ModifiedOn
- State written on every `SetLedColor()` call via `WriteStateAsync()`

## Current Limitations

- LED control simplified to on/off for onboard LED (RGB support structure exists but commented out)
- No dedicated test projects; system designed for manual testing
- Uses development clustering (not production-ready membership persistence)
- No authentication/authorization implemented
- Terminal.Gui client is console-only (no web UI)
