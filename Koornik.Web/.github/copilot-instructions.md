# Koornik.Web - Copilot Instructions

## Project Overview

React 19 + TypeScript frontend for the Koornik application. Built with Vite 7, using Bun as the package manager.

## Tech Stack

- **UI Framework**: React 19 with TypeScript (strict mode enabled)
- **Build Tool**: Vite 7 with `@vitejs/plugin-react`
- **Package Manager**: Bun (`bun add`, `bun install`, `bun run`)
- **UI Components**: Ant Design (antd v6)
- **Maps**: Leaflet + react-leaflet v5
- **Real-time**: SignalR (`@microsoft/signalr`)

## Developer Commands

```bash
bun dev          # Start dev server with HMR
bun run build    # TypeScript check + Vite production build
bun run lint     # ESLint check
bun run preview  # Preview production build
```

## Project Structure

```
src/
├── main.tsx     # App entry point with StrictMode
├── App.tsx      # Root component
├── assets/      # Static assets (SVGs, images)
├── index.css    # Global styles
└── App.css      # Component styles
public/          # Static files served as-is
```

## Code Conventions

### TypeScript
- Strict mode enabled with `noUnusedLocals` and `noUnusedParameters`
- Target ES2022, use modern JavaScript features
- Import `.tsx` extensions explicitly when importing local modules

### Components
- Use functional components with hooks (no class components)
- Prefer Ant Design components over custom UI implementations
- Maps: Use `react-leaflet` components, types from `@types/leaflet`

### Styling
- CSS files colocated with components (`Component.css` pattern)
- Global styles in `index.css`

## Integration Points

- **SignalR Hub**: Real-time communication with backend (connection setup TBD)
- **Leaflet Maps**: Configure tile providers and map options as needed

## ESLint Configuration

Uses flat config (`eslint.config.js`) with:
- TypeScript-ESLint recommended rules
- React Hooks rules (enforced)
- React Refresh rules for Vite HMR compatibility
