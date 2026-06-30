Tech stack

Monorepo: mobile (Expo / React Native), web (Next.js 15), backend (ASP.NET Core / EF Core / PostgreSQL), infra (Terraform, Docker, GitHub Actions).

Conventions:

EF Core never auto-migrates in production — generate a SQL script and run it manually.