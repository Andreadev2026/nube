import axios from 'axios';

// En Vercel se define VITE_API_URL (URL pública del backend en Railway); en local, el backend corre en 5085
export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5085';

const api = axios.create({
  baseURL: `${API_URL}/api`
});

export default api;
