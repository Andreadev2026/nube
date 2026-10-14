import axios from 'axios';

// En Vercel se define VITE_API_URL (URL pública del backend en Railway); en local, el backend corre en 5085
export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5085';

const api = axios.create({
  baseURL: `${API_URL}/api`
});

// La API exige el JWT del login en todas las llamadas (salvo el login mismo)
let token = null;
let alExpirarSesion = null;

/** App lo llama al iniciar sesión (token + callback para un 401) y al cerrarla (null). */
export function configurarSesion(nuevoToken, onExpira = null) {
  token = nuevoToken;
  alExpirarSesion = onExpira;
}

api.interceptors.request.use(config => {
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Token vencido o inválido: se cierra la sesión y el login vuelve a la misma URL
api.interceptors.response.use(
  r => r,
  err => {
    if (err.response?.status === 401 && token && alExpirarSesion) alExpirarSesion();
    return Promise.reject(err);
  }
);

export default api;
