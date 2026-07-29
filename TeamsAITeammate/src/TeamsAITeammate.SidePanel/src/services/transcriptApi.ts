import { authentication } from '@microsoft/teams-js';

export interface SpeechAuthorization {
  token: string;
  region: string;
  expiresAt: string;
}

export interface ClientTranscriptSegment {
  id: string;
  meetingId: string;
  text: string;
  language: string;
  timestamp: string;
  durationMs: number;
  confidence: number;
}

async function getHeaders(): Promise<HeadersInit> {
  const token = await authentication.getAuthToken();
  return {
    Authorization: `Bearer ${token}`,
    'Content-Type': 'application/json',
  };
}

export async function getSpeechAuthorization(): Promise<SpeechAuthorization> {
  const response = await fetch('/api/speech/token', {
    headers: await getHeaders(),
  });
  if (!response.ok) {
    throw new Error(`Speech authorization failed: ${response.status}`);
  }
  return response.json();
}

export async function submitTranscriptSegment(segment: ClientTranscriptSegment): Promise<void> {
  const response = await fetch('/api/transcript/segments', {
    method: 'POST',
    headers: await getHeaders(),
    body: JSON.stringify(segment),
  });
  if (!response.ok) {
    throw new Error(`Transcript submission failed: ${response.status}`);
  }
}