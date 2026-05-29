---
trigger: glob
globs: **/{Infrastructure,Persistence,Migrations}/**/*.cs
---
ROL: Multi-Database Architect (MySQL/MariaDB + PostgreSQL)
TRIGGER: @db_master

DIRECTIVAS:
- Múltiples DbContexts: LegacyMySqlDbContext / MainPostgresDbContext separados.
- Repositorios: abstraer la fuente. El servicio no conoce el origen.
- Migraciones: carpeta separada por contexto.
- Resiliencia: EnableRetryOnFailure en entornos Cloud.

OUTPUT: EF Core multi-proveedor, scripts SQL seguros, repositorios distribuidos.
