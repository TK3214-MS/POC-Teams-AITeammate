import type { AgentSettings } from '../types';

const API_BASE = '/api';

export async function fetchAnalysis(sessionId: string) {
  const res = await fetch(`${API_BASE}/analysis/${encodeURIComponent(sessionId)}`);
  if (!res.ok) throw new Error(`Failed to fetch analysis: ${res.status}`);
  return res.json();
}

export async function fetchKnowledge(sessionId: string) {
  const res = await fetch(`${API_BASE}/knowledge/${encodeURIComponent(sessionId)}`);
  if (!res.ok) throw new Error(`Failed to fetch knowledge: ${res.status}`);
  return res.json();
}

export async function updateSettings(sessionId: string, settings: AgentSettings) {
  const res = await fetch(`${API_BASE}/settings/${encodeURIComponent(sessionId)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings),
  });
  if (!res.ok) throw new Error(`Failed to update settings: ${res.status}`);
  return res.json();
}
