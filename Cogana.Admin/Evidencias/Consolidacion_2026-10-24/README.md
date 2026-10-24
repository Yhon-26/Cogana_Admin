# Evidencias locales de consolidación

Los archivos de esta carpeta corresponden a comprobaciones locales del 24/10/2026. Las capturas utilizan cuentas, pedidos, referencias, importes y destinatarios simulados. No acreditan compras, pagos ni entregas reales.

- `escritorio.txt`: arnés WPF completo.
- `acceso.txt`: pruebas de autenticación con HTTP simulado.
- `funcion-usuarios.txt`: código local de Edge con Auth/BD simulados.
- `operaciones.txt`: regresiones de infraestructura y comparación con firmas de RPC leídas del backend.
- `onboarding.txt`: navegación y preferencia local aislada.
- `restore-bloqueado.txt`, `restore-arneses.txt`: restauración de dependencias con lockfiles.
- `build-release.txt`, `empaquetado.txt`: compilación y preparación del paquete local.
- `canal-publico.json`: consulta pública de la release vigente; no contiene credenciales.
- `publicacion.txt`, `paquetes.json`: publicación 1.13.0 y comparación de tamaños/huellas SHA-256 con GitHub.
- `RELEASES.publicado`, `releases.win.json.publicado`: feeds descargados del canal público y contrastados con los paquetes de esta release.
- `consolidacion-*.png`: capturas de ventanas aisladas. La versión de la barra inferior pertenece al ejecutable de pruebas.

El informe principal distingue implementación, pruebas automatizadas y validación operativa pendiente.
