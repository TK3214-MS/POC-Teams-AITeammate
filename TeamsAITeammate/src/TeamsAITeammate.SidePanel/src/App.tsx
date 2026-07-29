import React, { useState } from 'react';
import {
  FluentProvider,
  teamsDarkTheme,
  teamsLightTheme,
  Tab,
  TabList,
  Text,
  Badge,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { useTeamsContext } from './hooks/useTeamsContext';
import { useSignalR } from './hooks/useSignalR';
import { AnalysisDashboard } from './components/Dashboard/AnalysisDashboard';
import { KnowledgeList } from './components/Knowledge/KnowledgeList';
import { QuestionQueue } from './components/Questions/QuestionQueue';
import { MeetingSummary } from './components/Summary/MeetingSummary';
import { AgentSettings } from './components/Settings/AgentSettings';
import type { AgentSettings as AgentSettingsType } from './types';

const useStyles = makeStyles({
  root: { height: '100vh', display: 'flex', flexDirection: 'column', overflow: 'hidden' },
  header: {
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS,
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  content: { flex: 1, overflow: 'auto', padding: tokens.spacingVerticalS },
  tabContent: { padding: tokens.spacingVerticalS },
});

type TabKey = 'dashboard' | 'knowledge' | 'questions' | 'summary' | 'settings';

const App: React.FC = () => {
  const styles = useStyles();
  const teamsContext = useTeamsContext();
  const signalR = useSignalR(teamsContext.meetingId);
  const [activeTab, setActiveTab] = useState<TabKey>('dashboard');
  const isDark = window.matchMedia('(prefers-color-scheme: dark)').matches;

  const defaultSettings: AgentSettingsType = {
    silenceThreshold: 30,
    periodicInterval: 300,
    enableProactiveIntervention: true,
    maxInterventionsPerMeeting: 20,
  };

  const handleSaveSettings = (_settings: AgentSettingsType) => {
    // Would call updateSettings API here
    console.log('Settings saved:', _settings);
  };

  return (
    <FluentProvider theme={isDark ? teamsDarkTheme : teamsLightTheme}>
      <div className={styles.root}>
        {/* Header */}
        <div className={styles.header}>
          <Text weight="bold" size={500}>🤖 AI Teammate</Text>
          <Badge
            appearance="filled"
            color={signalR.connected ? 'success' : 'danger'}
            size="small"
          >
            {signalR.connected ? 'Connected' : 'Disconnected'}
          </Badge>
        </div>

        {/* Tab navigation */}
        <TabList
          selectedValue={activeTab}
          onTabSelect={(_e, data) => setActiveTab(data.value as TabKey)}
        >
          <Tab value="dashboard">Dashboard</Tab>
          <Tab value="knowledge">Knowledge ({signalR.knowledge.length})</Tab>
          <Tab value="questions">Questions ({signalR.questions.length})</Tab>
          <Tab value="summary">Summary</Tab>
          <Tab value="settings">Settings</Tab>
        </TabList>

        {/* Tab content */}
        <div className={styles.content}>
          {activeTab === 'dashboard' && (
            <AnalysisDashboard
              analysis={signalR.analysis}
              speakerStats={[]}
              meetingId={teamsContext.meetingId}
            />
          )}
          {activeTab === 'knowledge' && (
            <KnowledgeList entries={signalR.knowledge.map(k => ({
              id: k.id,
              title: k.content.slice(0, 50),
              content: k.content,
              type: k.category,
              tags: k.relatedTopics,
              confidenceScore: k.confidence,
              createdAt: new Date().toISOString(),
            }))} />
          )}
          {activeTab === 'questions' && (
            <QuestionQueue questions={signalR.questions} />
          )}
          {activeTab === 'summary' && (
            <MeetingSummary analysis={signalR.analysis} />
          )}
          {activeTab === 'settings' && (
            <AgentSettings initialSettings={defaultSettings} onSave={handleSaveSettings} />
          )}
        </div>
      </div>
    </FluentProvider>
  );
};

export default App;
