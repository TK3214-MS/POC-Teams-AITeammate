export interface AgentSettings {
  tenantId: string;
  intervention: InterventionSettings;
  questionGeneration: QuestionGenerationSettings;
  dataStore: DataStoreSettings;
  language: LanguageSettings;
  meetingFilter: MeetingFilterSettings;
  updatedAt: string;
  updatedBy: string;
}

export interface InterventionSettings {
  frequency: 'low' | 'medium' | 'high';
  silenceThresholdSeconds: number;
  maxInterventionsPerMeeting: number;
  enableProactiveIntervention: boolean;
  cooldownSeconds: number;
}

export interface QuestionGenerationSettings {
  enabledCategories: string[];
  maxQuestionsPerIntervention: number;
  priorityThreshold: string;
}

export interface DataStoreSettings {
  primaryProvider: 'Dataverse' | 'CosmosDB' | 'AzureAISearch' | 'SharePoint';
  enableRAG: boolean;
  ragMinRelevanceScore: number;
}

export interface LanguageSettings {
  autoDetect: boolean;
  preferredLanguage: string;
  supportedLanguages: string[];
}

export interface MeetingFilterSettings {
  includeAllMeetings: boolean;
  includedOrganizers: string[];
  excludedMeetingPatterns: string[];
  minimumParticipants: number;
}

export interface DashboardStats {
  tenantId: string;
  totalKnowledgeEntries: number;
  totalMeetingSessions: number;
  totalAnalysisExecutions: number;
  activeUsers: number;
  knowledgeByCategory: Record<string, number>;
  aiCost: {
    totalPromptTokens: number;
    totalCompletionTokens: number;
    estimatedCostUsd: number;
  };
}

export interface KnowledgeEntry {
  id: string;
  tenantId: string;
  title: string;
  content: string;
  summary: string;
  category: string;
  status: string;
  tags: string[];
  createdAt: string;
  updatedAt?: string;
  confidenceScore: number;
  sourceSpeaker: string;
  meetingSubject: string;
}

export interface TenantUser {
  userId: string;
  tenantId: string;
  displayName: string;
  email: string;
  role: 'Viewer' | 'User' | 'Admin';
  stats: {
    meetingsAttended: number;
    knowledgeContributed: number;
    questionsAnswered: number;
    lastActiveAt?: string;
  };
}
