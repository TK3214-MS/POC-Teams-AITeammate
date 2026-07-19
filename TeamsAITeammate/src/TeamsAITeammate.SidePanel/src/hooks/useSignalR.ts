import { useState, useEffect, useCallback } from 'react';
import { startConnection, stopConnection } from '../services/signalrClient';
import type { ConversationAnalysis, DetectedTopic, TacitKnowledgeCandidate, GeneratedQuestion } from '../types';

interface SignalRState {
  connected: boolean;
  topics: DetectedTopic[];
  knowledge: TacitKnowledgeCandidate[];
  questions: GeneratedQuestion[];
  analysis: ConversationAnalysis | null;
  summaryHtml: string | null;
}

export function useSignalR(meetingId: string | null) {
  const [state, setState] = useState<SignalRState>({
    connected: false,
    topics: [],
    knowledge: [],
    questions: [],
    analysis: null,
    summaryHtml: null,
  });

  const connect = useCallback(async () => {
    if (!meetingId) return;

    try {
      const conn = await startConnection(meetingId);

      conn.on('topicDetected', (topic: DetectedTopic) => {
        setState(prev => ({
          ...prev,
          topics: [...prev.topics.filter(t => t.id !== topic.id), topic],
        }));
      });

      conn.on('knowledgeExtracted', (item: TacitKnowledgeCandidate) => {
        setState(prev => ({
          ...prev,
          knowledge: [...prev.knowledge.filter(k => k.id !== item.id), item],
        }));
      });

      conn.on('questionGenerated', (question: GeneratedQuestion) => {
        setState(prev => ({
          ...prev,
          questions: [...prev.questions.filter(q => q.id !== question.id), question],
        }));
      });

      conn.on('analysisUpdated', (analysis: ConversationAnalysis) => {
        setState(prev => ({ ...prev, analysis }));
      });

      conn.on('summaryUpdated', (summary: string) => {
        setState(prev => ({ ...prev, summaryHtml: summary }));
      });

      setState(prev => ({ ...prev, connected: true }));
    } catch (err) {
      console.error('SignalR connection failed:', err);
    }
  }, [meetingId]);

  useEffect(() => {
    connect();
    return () => { stopConnection(); };
  }, [connect]);

  return state;
}
