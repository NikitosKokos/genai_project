// Simple API client wrapper
// We use the Next.js proxy at /api to avoid CORS issues
const API_BASE_URL = '/api';

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
  return text ? JSON.parse(text) : ({} as T);
}

export const api = {
  // Financial Advisor / RAG
  chat: {
    sendMessage: async (message: string, context?: any) => {
      return fetchJson('/rag/query', {
        method: 'POST',
        body: JSON.stringify({ query: message, userId: 1 }), // Hardcoded userId for demo
      });
    },
    streamMessage: async (message: string, sessionId: string, enableReasoning: boolean = false, onChunk: (chunk: string) => void) => {
      const response = await fetch(`${API_BASE_URL}/chat/stream`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message, sessionId, enableReasoning }),
      });

      if (!response.body) return;

      const reader = response.body.getReader();
      const decoder = new TextDecoder();

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        const chunk = decoder.decode(value, { stream: true });
        onChunk(chunk);
      }
    }
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
