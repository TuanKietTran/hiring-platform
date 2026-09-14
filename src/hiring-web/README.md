# Hirelane web

Angular 22 standalone SPA written in strict TypeScript. It uses signals for session/view state, typed HTTP contracts in `src/app/core/models.ts`, and relative `/api` requests proxied to the Aspire API during development.

Run through the repository's Aspire AppHost (`../../README.md`) or independently:

```bash
npm install
npm start   # API fallback: http://localhost:5100
npm test
npm run build
```
