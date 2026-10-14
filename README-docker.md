# NODUS con Docker Compose

## Requisitos
- Docker Desktop (o Docker Engine) con Docker Compose v2

## Configuración
```bash
cp .env.example .env
```
Editar `.env` y reemplazar las passwords y `JWT_KEY` por valores propios. `.env` está en `.gitignore`: no subirlo nunca.

## Levantar
```bash
docker compose up -d --build
```
- Aplicación: http://localhost (o el puerto de `WEB_PORT`)
- pgAdmin: http://localhost:5050 (solo desde esta máquina). Para conectarse a la base: host `db`, puerto `5432`, usuario y password de `.env`.

La API no publica puertos: se accede por `http://localhost/api/...` a través de nginx. Las migraciones se aplican solas al arrancar.

## Bajar
```bash
docker compose down
```

## Ver logs
```bash
docker compose logs -f          # todos los servicios
docker compose logs -f api      # solo la API
```

## Resetear la base
Borra **todos** los datos (base, configuración de pgAdmin y archivos subidos):
```bash
docker compose down -v
docker compose up -d --build
```

## HTTPS para probar desde el celular
La cámara del escáner QR solo funciona con conexión segura: `http://localhost` sirve en la PC, pero desde el celular hace falta HTTPS.

1. Ver la IP de la PC en la red Wi-Fi (`ipconfig`, "Dirección IPv4"), por ejemplo `192.168.0.35`.
2. Generar un certificado autofirmado para esa IP (queda en `certs/`, que está en `.gitignore`):
   ```bash
   mkdir -p certs
   MSYS_NO_PATHCONV=1 openssl req -x509 -newkey rsa:2048 -nodes -days 365 \
     -keyout certs/nodus.key -out certs/nodus.crt -subj "/CN=NODUS local" \
     -addext "subjectAltName=IP:192.168.0.35,IP:127.0.0.1,DNS:localhost"
   ```
   (`MSYS_NO_PATHCONV=1` solo hace falta en Git Bash de Windows.) Si la IP cambia, regenerarlo y reiniciar `web`.
3. Levantar con el override de HTTPS:
   ```bash
   docker compose -f docker-compose.yml -f docker-compose.https.yml up -d
   ```
4. En el celular, conectado a la misma Wi-Fi, abrir `https://192.168.0.35`. El navegador avisa que el certificado no es confiable: elegir "Avanzado" → "Continuar". Después la cámara pide permiso normalmente.

Para volver a solo HTTP: `docker compose up -d` (sin el override).
