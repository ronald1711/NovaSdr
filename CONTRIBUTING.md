# Contributing to PantheonSDR

Thank you for your interest in PantheonSDR!

## Code of Conduct

Be respectful. Amateur radio community values: share knowledge, collaborate, experiment.

## How to Contribute

### Bug Reports
Use the GitHub Issues **Bug Report** template. Include:
- OS and version
- Hardware (Brick2, SDRplay RSP1A, PlutoSDR Plus, etc.)
- Steps to reproduce
- Expected vs actual behaviour

### Feature Requests
Use the **Feature Request** template. Describe the use case from an operator perspective.

### Code Contributions

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Write code + tests
4. Ensure all existing tests pass
5. Open a Pull Request with a clear description

## Development Setup

### Requirements
- .NET 10 SDK
- Node.js 20+
- Git

### Backend
```bash
cd src/PantheonSDR
dotnet run
```

### Frontend
```bash
cd src/zeus-web
npm install
npm run dev
```

## Code Style

- C#: .NET naming conventions, nullable enabled, no `var` for non-obvious types
- TypeScript: strict mode, no `any`
- Comments: only when the **why** is non-obvious — never explain what the code does

## Hardware-Specific Development

- **SDRplay:** Install SDRplay API 3.x from [sdrplay.com/api](https://www.sdrplay.com/api/)
- **PlutoSDR Plus:** Install libiio; default IP `192.168.2.1`
- **Brick2/HL2:** Standard OpenHPSDR network setup

## License

By contributing, you agree that your contributions will be released under GPL-2.0-or-later.
