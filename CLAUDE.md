# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Blazor WebAssembly client application for Toxiq, a social media platform. It's built with .NET 8 and includes features for posts, comments, chat, notifications, and user authentication.

## Development Commands

### Build and Run
```bash
# Build the application
dotnet build

# Run the application (requires .NET 8 SDK)
dotnet run --project Toxiq.WebApp.Client

# Run without opening browser
dotnet run --project Toxiq.WebApp.Client --launch-profile no-browser-http
```

### Available Launch Profiles
- `http` - HTTP with browser launch (port 9454)
- `https` - HTTPS with browser launch (ports 7162/5116)
- `no-browser-http` - HTTP without browser launch (port 9454)
- `IIS Express` - IIS Express profile

## Architecture

### Core Structure
The application follows a layered architecture with clear separation of concerns:

- **Program.cs**: DI container configuration and service registration
- **Services/**: Business logic and API communication
- **Components/**: Reusable Blazor components
- **Pages/**: Top-level page components
- **Domain/**: Domain models and mappers
- **Extensions/**: Service registration extensions

### Key Services

#### Authentication
- Multiple authentication providers (Telegram, Manual)
- Token-based authentication with local storage
- Authentication state management with events

#### API Layer
- `OptimizedApiService`: Main API service implementation
- Service interfaces for different domains (Auth, Posts, Comments, Users, etc.)
- HTTP client configuration with base URL from appsettings

#### Chat System
- Domain-driven chat implementation
- SignalR integration for real-time messaging
- Configurable chat options (page size, message limits, etc.)

#### Caching
- Multi-layer caching with IndexedDB and memory cache
- Service-specific caching strategies

### Configuration

#### appsettings.json Structure
- `ApiBaseUrl`: Backend API endpoint
- `Feed`: Feed pagination and performance settings
- `Performance`: Request limits and timeouts
- `UI`: Theme and animation configuration

#### Service Registration Pattern
Services are registered through extension methods:
- `AddChatServices()`: Chat-related services
- `AddFeedServices()`: Feed functionality
- `AddApiServices()`: API layer services
- `AddSignalRHubGateway()`: Real-time communication

### Dependencies
- Blazor WebAssembly (.NET 8)
- Microsoft FluentUI components
- SignalR for real-time features
- Blazored.LocalStorage for client-side storage
- Custom DTO library: `Toxiq.Mobile.Dto`

### Development Notes
- The application uses `InvariantGlobalization` for performance
- Lazy loading is enabled for better performance
- Compression is enabled for production builds
- SignalR hub URL is dynamically constructed from API base URL