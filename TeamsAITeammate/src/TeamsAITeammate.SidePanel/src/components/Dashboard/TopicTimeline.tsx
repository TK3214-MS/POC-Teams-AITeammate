import React from 'react';
import {
  Card,
  CardHeader,
  Text,
  Badge,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { DetectedTopic } from '../../types';

const useStyles = makeStyles({
  container: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  topicCard: { padding: tokens.spacingVerticalS },
  keyTerms: { display: 'flex', gap: tokens.spacingHorizontalXS, flexWrap: 'wrap', marginTop: tokens.spacingVerticalXS },
});

interface Props {
  topics: DetectedTopic[];
}

const statusIcon: Record<string, string> = {
  Active: '🟢',
  Concluded: '✅',
  Tabled: '⏸️',
};

export const TopicTimeline: React.FC<Props> = ({ topics }) => {
  const styles = useStyles();

  return (
    <div className={styles.container}>
      {topics.map(topic => (
        <Card key={topic.id} className={styles.topicCard} size="small">
          <CardHeader
            header={<Text weight="semibold">{statusIcon[topic.status] ?? '⚪'} {topic.title}</Text>}
            description={<Text size={200}>{topic.summary}</Text>}
          />
          <div className={styles.keyTerms}>
            {topic.keyTerms.map(term => (
              <Badge key={term} appearance="outline" size="small">{term}</Badge>
            ))}
          </div>
        </Card>
      ))}
      {topics.length === 0 && <Text italic>トピックはまだ検出されていません</Text>}
    </div>
  );
};
