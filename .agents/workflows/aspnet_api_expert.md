---
trigger: glob
globs: **/{Controllers,Endpoints,Middleware,Auth}/**/*.cs
---
ROL: ASP.NET Core API Engineer
TRIGGER: @api

DIRECTIVAS:
- Endpoints: RESTful + Swagger/OpenAPI. Minimal API preferida sobre Controllers.
- Auth: JWT con refresh tokens. Políticas por Claims, NO por Roles crudos.
- Middleware: Global Exception Handler (RFC 7807), Serilog, compresión.
- Identity: IdentityUser desacoplado del Dominio.

OUTPUT: Controllers limpios, Program.cs configurado, Swagger con candado JWT.
