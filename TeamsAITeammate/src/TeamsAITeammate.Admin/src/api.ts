import type { AgentSettings, DashboardStats, KnowledgeEntry, TenantUser } from './types';

const BASE = '/api/admin';

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, {
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  });
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json() as Promise<T>;
}

export const api = {
  getDashboard: () => fetchJson<DashboardStats>(`${BASE}/dashboard`),

  getSettings: () => fetchJson<AgentSettings>(`${BASE}/settings`),
  updateSettings: (settings: AgentSettings) =>
    fetchJson<AgentSettings>(`${BASE}/settings`, {
      method: 'PUT',
      body: JSON.stringify(settings),
    }),

  getKnowledge: (query?: string) =>
    fetchJson<KnowledgeEntry[]>(`${BASE}/knowledge${query ? `?query=${encodeURIComponent(query)}` : ''}`),
  getKnowledgeEntry: (id: string) => fetchJson<KnowledgeEntry>(`${BASE}/knowledge/${id}`),
  createKnowledge: (entry: Partial<KnowledgeEntry>) =>
    fetchJson<KnowledgeEntry>(`${BASE}/knowledge`, {
      method: 'POST',
      body: JSON.stringify(entry),
    }),
  updateKnowledge: (id: string, entry: Partial<KnowledgeEntry>) =>
    fetchJson<KnowledgeEntry>(`${BASE}/knowledge/${id}`, {
      method: 'PUT',
      body: JSON.stringify(entry),
    }),
  deleteKnowledge: (id: string) =>
    fetch(`${BASE}/knowledge/${id}`, { method: 'DELETE' }),

  getUsers: () => fetchJson<TenantUser[]>(`${BASE}/users`),
  updateUserRole: (userId: string, role: string) =>
    fetchJson<TenantUser>(`${BASE}/users/${userId}/role`, {
      method: 'PUT',
      body: JSON.stringify({ role }),
    }),
};
