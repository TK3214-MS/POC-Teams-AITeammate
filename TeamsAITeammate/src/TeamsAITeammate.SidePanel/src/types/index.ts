export interface DetectedTopic {
  id: string;
  title: string;
  summary: string;
  firstMentionedAt: string;
  lastMentionedAt: string;
  status: 'Active' | 'Concluded' | 'Tabled';
  discussionDepth: number;
  keyTerms: string[];
  involvedSpeakers: string[];
}

export interface TacitKnowledgeCandidate {
  id: string;
  category: string;
  content: string;
  context: string;
  sourceSpeaker: string;
  confidence: number;
  relatedTopics: string[];
  requiresValidation: boolean;
}

export interface GeneratedQuestion {
  id: string;
  question: string;
  type: string;
  priority: 'Critical' | 'High' | 'Medium' | 'Low';
  rationale: string;
  targetSpeaker: string;
  relatedTopicId: string;
}

export interface ActionItem {
  id: string;
  description: string;
  assignee: string;
  dueDate?: string;
  status: 'Open' | 'InProgress' | 'Completed' | 'Cancelled';
  relatedTopicId: string;
}

export interface DetectedDecision {
  id: string;
  summary: string;
  context: string;
  decisionMakers: string[];
  detectedAt: string;
  confidence: number;
}

export interface ConversationAnalysis {
  topics: DetectedTopic[];
  tacitKnowledgeCandidates: TacitKnowledgeCandidate[];
  questions: GeneratedQuestion[];
  suggestedAgenda: SuggestedAgendaItem[];
  decisions: DetectedDecision[];
  actionItems: ActionItem[];
}

export interface SuggestedAgendaItem {
  id: string;
  title: string;
  rationale: string;
  priority: 'Critical' | 'High' | 'Medium' | 'Low';
}

export interface SpeakerStats {
  speakerId: string;
  speakerName: string;
  segmentCount: number;
  totalSpeakingTime: string;
  lastSpokenAt: string;
}

export interface KnowledgeEntry {
  id: string;
  title: string;
  content: string;
  type: string;
  tags: string[];
  confidenceScore: number;
  createdAt: string;
}

export interface AgentSettings {
  silenceThreshold: number;
  periodicInterval: number;
  enableProactiveIntervention: boolean;
  maxInterventionsPerMeeting: number;
}
