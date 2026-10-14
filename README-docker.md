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
Se usa una CA local (instalada una sola vez en el celular) que firma el certificado del servidor. Todo queda en `certs/`, que está en `.gitignore`.

1. Datos de la PC: nombre (`hostname`, por ejemplo `TuCla` → `TuCla.local`) e IP en la Wi-Fi (`ipconfig`, "Dirección IPv4").
2. Crear la CA, una sola vez. Va sin restricciones de nombres porque iOS rechaza la CA si las tiene: su clave
   (`nodus-ca.key`) nunca tiene que salir de esta PC, y conviene quitar el perfil del celular al terminar las pruebas:
   ```bash
   cd certs
   export MSYS_NO_PATHCONV=1   # solo en Git Bash de Windows
   openssl req -x509 -newkey rsa:2048 -nodes -days 825 -sha256 -keyout nodus-ca.key -out nodus-ca.crt \
     -subj "/CN=NODUS CA local 2 (TuCla)" \
     -addext "basicConstraints=critical,CA:TRUE,pathlen:0" -addext "keyUsage=critical,keyCertSign,cRLSign" \
     -addext "subjectKeyIdentifier=hash"
   ```
3. Crear el certificado del servidor (repetir solo esto si cambia la IP, sin reinstalar nada en el celular). En `servidor.ext` poner:
   ```
   basicConstraints=critical,CA:FALSE
   keyUsage=critical,digitalSignature,keyEncipherment
   extendedKeyUsage=serverAuth
   subjectKeyIdentifier=hash
   authorityKeyIdentifier=keyid
   subjectAltName=DNS:TuCla.local,DNS:tucla.local,DNS:localhost,IP:127.0.0.1,IP:<IP de la PC>
   ```
   ```bash
   openssl req -newkey rsa:2048 -nodes -keyout nodus.key -out nodus.csr -subj "/CN=TuCla.local"
   openssl x509 -req -in nodus.csr -CA nodus-ca.crt -CAkey nodus-ca.key -CAcreateserial -days 397 -sha256 \
     -extfile servidor.ext -out nodus.crt
   ```
4. Levantar con el override de HTTPS (siempre con los dos `-f`; sin el override se pierde el puerto 443):
   ```bash
   docker compose -f docker-compose.yml -f docker-compose.https.yml up -d --build web
   ```
5. Firewall de Windows: la red Wi-Fi en "Privada" y el puerto 443 abierto (PowerShell como administrador):
   ```powershell
   New-NetFirewallRule -DisplayName "NODUS 443" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow -Profile Any
   ```
6. Instalar la CA en el celular, una sola vez, descargándola de `http://<IP de la PC>/nodus-ca.crt`:
   - **iPhone** (con Safari): permitir la descarga → Ajustes → "Perfil descargado" → Instalar. Después Ajustes → General → Información → Ajustes de confianza de certificados → activar "NODUS CA local 2 (TuCla)".
   - **Android**: Ajustes → Seguridad → Más ajustes → Cifrado y credenciales → Instalar un certificado → Certificado de CA → elegir el archivo descargado.
7. Abrir `https://TuCla.local` (o `https://<IP de la PC>`) en el celular, en la misma Wi-Fi.

Para quitar la CA del iPhone: Ajustes → General → VPN y gestión de dispositivos → el perfil → Eliminar.
Si un antivirus inspecciona HTTPS en la PC (por ejemplo Avast Web Shield), las pruebas con curl desde la PC muestran su certificado y no el de NODUS: probar desde el celular.

Para volver a solo HTTP: `docker compose up -d` (sin el override).
