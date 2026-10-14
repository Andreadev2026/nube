# Etapa 1: build de la app con Vite
FROM node:lts-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

# Vite incrusta VITE_API_URL en el bundle al compilar: con "/api" las llamadas van al mismo
# origen y nginx las reenvía al contenedor de la API (ver nginx.conf)
ARG VITE_API_URL=/api
ENV VITE_API_URL=$VITE_API_URL

# Dirección con la que los celulares llegan al sistema (ej. http://192.168.x.x o https://nodus.dominio):
# se usa para armar la URL de los QR de los equipos. Vacía = el origen desde el que se abre la app.
ARG VITE_PUBLIC_BASE_URL=
ENV VITE_PUBLIC_BASE_URL=$VITE_PUBLIC_BASE_URL

COPY . .
RUN npm run build

# Etapa 2: nginx sirviendo los archivos estáticos
FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
