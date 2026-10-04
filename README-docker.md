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
