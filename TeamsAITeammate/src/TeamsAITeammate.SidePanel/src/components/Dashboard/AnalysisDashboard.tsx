import React from 'react';
import {
  Card,
  CardHeader,
  Text,
  Badge,
  Divider,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { TopicTimeline } from './TopicTimeline';
import type { ConversationAnalysis, SpeakerStats as SpeakerStatsType } from '../../types';
import { SpeakerStats } from './SpeakerStats';

const useStyles = makeStyles({
  container: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM, padding: tokens.spacingVerticalS },
  statsRow: { display: 'flex', gap: tokens.spacingHorizontalM, flexWrap: 'wrap' },
  statCard: { flex: '1 1 120px', padding: tokens.spacingVerticalS },
});

interface Props {
  analysis: ConversationAnalysis | null;
  speakerStats: SpeakerStatsType[];
}

export const AnalysisDashboard: React.FC<Props> = ({ analysis, speakerStats }) => {
  const styles = useStyles();

  return (
    <div className={styles.container} data-testid="analysis-dashboard">
      {/* Summary stats */}
      <div className={styles.statsRow}>
        <Card className={styles.statCard} size="small">
          <CardHeader header={<Text weight="semibold">📋 Topics</Text>} />
          <Badge size="large" appearance="filled" color="brand">{analysis?.topics.length ?? 0}</Badge>
        </Card>
        <Card className={styles.statCard} size="small">
          <CardHeader header={<Text weight="semibold">📚 Knowledge</Text>} />
          <Badge size="large" appearance="filled" color="success">{analysis?.tacitKnowledgeCandidates.length ?? 0}</Badge>
        </Card>
        <Card className={styles.statCard} size="small">
          <CardHeader header={<Text weight="semibold">❓ Questions</Text>} />
          <Badge size="large" appearance="filled" color="warning">{analysis?.questions.length ?? 0}</Badge>
        </Card>
      </div>

      <Divider />

      {/* Topic timeline */}
      <Text weight="semibold" size={400}>トピックタイムライン</Text>
      <TopicTimeline topics={analysis?.topics ?? []} />

      <Divider />

      {/* Speaker stats */}
      <Text weight="semibold" size={400}>話者統計</Text>
      <SpeakerStats stats={speakerStats} />
    </div>
  );
};
