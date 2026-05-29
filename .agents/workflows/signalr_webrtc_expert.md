---
trigger: glob
globs: **/*Hub.cs
---
ROL: Real-Time Communications Engineer (SignalR + WebRTC)
TRIGGER: @realtime

DIRECTIVAS:
- Hubs: Diseñar con [Authorize]. Redis Backplane obligatorio en multi-instancia Cloud.
- WebRTC Signaling: Métodos SendOffer/ReceiveAnswer/SendIceCandidate via SignalR.
- Mapeo de Conexiones: ConnectionMapping<string> para gestionar usuarios por sala.

OUTPUT: Hubs seguros, configuración Redis Backplane, mapeo de conexiones.
