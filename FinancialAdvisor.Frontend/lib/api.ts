// Simple API client wrapper
// In the future, this will be configured via environment variables
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

export interface ApiResponse<T> {
  data?: T;
  error?: string;
}

async function fetchJson<T>(endpoint: string, options?: RequestInit): Promise<T> {
  const url = `${API_BASE_URL}${endpoint}`;
  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });

  if (!response.ok) {
    throw new Error(`API error: ${response.statusText}`);
  }

  // Handle empty responses
  const text = await response.text();
  return text ? JSON.parse(text) : {};
}

export const api = {
  // Financial Advisor / RAG
  chat: {
    sendMessage: async (message: string, context?: any) => {
      return fetchJson('/advisor/chat', {
        method: 'POST',
        body: JSON.stringify({ message, context }),
      });
    },
  },

  // Dashboard Data
  dashboard: {
    getNews: async () => fetchJson('/dashboard/news'),
    getPortfolio: async () => fetchJson('/dashboard/portfolio'),
    getAssets: async () => fetchJson('/dashboard/assets'),
  },

  // Trading
  trading: {
    placeOrder: async (symbol: string, side: 'buy' | 'sell', quantity: number) => {
      return fetchJson('/trading/orders', {
        method: 'POST',
        body: JSON.stringify({ symbol, side, quantity }),
      });
    },
  },
};
