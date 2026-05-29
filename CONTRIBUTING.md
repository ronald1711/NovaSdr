# Bijdragen aan NovaSdr

Bedankt voor je interesse in NovaSdr! Dit document beschrijft hoe je kunt bijdragen.

## Gedragscode

Wees respectvol naar andere bijdragers. Amateur radio gemeenschap waarden: kennis delen, samenwerken, experimenteren.

## Hoe bijdragen?

### Bugs rapporteren
Gebruik de GitHub Issues met het **Bug Report** template. Vermeld:
- OS en versie
- Hardware (Brick2, HL2, etc.)
- Stappen om het probleem te reproduceren
- Verwacht vs. werkelijk gedrag

### Feature requests
Gebruik het **Feature Request** template. Beschrijf de use case vanuit operator-perspectief.

### Code bijdragen

1. Fork de repository
2. Maak een feature branch: `git checkout -b feature/mijn-feature`
3. Schrijf code + tests
4. Zorg dat alle bestaande tests slagen
5. Open een Pull Request met een duidelijke beschrijving

## Development Setup

### Vereisten
- .NET 10 SDK
- Node.js 20+
- Git

### Backend starten
```bash
cd src/OpenhpsdrZeus
dotnet run
```

### Frontend starten
```bash
cd src/zeus-web
npm install
npm run dev
```

## Code Stijl

- C#: follow .NET naming conventions, nullable enabled
- TypeScript: strict mode, geen `any`
- Geen comments die uitleggen WAT de code doet — alleen WAAROM (niet-obvioust)

## Licentie

Door bij te dragen ga je akkoord dat je bijdragen worden uitgebracht onder GPL-2.0-or-later.
