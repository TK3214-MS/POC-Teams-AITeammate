import React from 'react';
import {
  Button,
  Card,
  CardHeader,
  Text,
  Badge,
  Divider,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { Mic24Regular } from '@fluentui/react-icons';
import { dialog } from '@microsoft/teams-js';
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
  meetingId: string | null;
}

export const AnalysisDashboard: React.FC<Props> = ({ analysis, speakerStats, meetingId }) => {
  const styles = useStyles();

  const openCaptureDialog = () => {
    if (!meetingId) return;
    const url = `${window.location.origin}/capture?meetingId=${encodeURIComponent(meetingId)}`;
    dialog.url.open({ url, size: { height: 560, width: 420 } });
  };

  return (
    <div className={styles.container} data-testid="analysis-dashboard">
      <Button
        appearance="primary"
        icon={<Mic24Regular />}
        disabled={!meetingId}
        onClick={openCaptureDialog}
      >
        リアルタイム分析を開始
      </Button>

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
