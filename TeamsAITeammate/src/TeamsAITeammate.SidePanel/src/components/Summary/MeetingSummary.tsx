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
import type { ConversationAnalysis } from '../../types';

const useStyles = makeStyles({
  container: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS, padding: tokens.spacingVerticalS },
  section: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS },
});

interface Props {
  analysis: ConversationAnalysis | null;
}

export const MeetingSummary: React.FC<Props> = ({ analysis }) => {
  const styles = useStyles();

  if (!analysis) {
    return <Text italic>サマリーはまだ生成されていません</Text>;
  }

  return (
    <div className={styles.container}>
      {/* Decisions */}
      {analysis.decisions.length > 0 && (
        <div className={styles.section}>
          <Text weight="semibold" size={400}>💡 意思決定事項</Text>
          {analysis.decisions.map(d => (
            <Card key={d.id} size="small">
              <CardHeader header={<Text>{d.summary}</Text>} />
            </Card>
          ))}
        </div>
      )}

      <Divider />

      {/* Action Items */}
      {analysis.actionItems.length > 0 && (
        <div className={styles.section}>
          <Text weight="semibold" size={400}>📋 アクションアイテム</Text>
          {analysis.actionItems.map(a => (
            <Card key={a.id} size="small">
              <CardHeader
                header={<Text>{a.description}</Text>}
                description={
                  <Badge appearance="outline" size="small">
                    {a.assignee || 'TBD'}
                  </Badge>
                }
              />
            </Card>
          ))}
        </div>
      )}
    </div>
  );
};
