import React from 'react';
import {
  Card,
  CardHeader,
  Text,
  Badge,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { GeneratedQuestion } from '../../types';

const useStyles = makeStyles({
  container: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  meta: { display: 'flex', gap: tokens.spacingHorizontalXS, marginTop: tokens.spacingVerticalXS },
});

const priorityColor: Record<string, 'danger' | 'warning' | 'success' | 'informative'> = {
  Critical: 'danger',
  High: 'warning',
  Medium: 'informative',
  Low: 'success',
};

interface Props {
  questions: GeneratedQuestion[];
}

export const QuestionQueue: React.FC<Props> = ({ questions }) => {
  const styles = useStyles();

  return (
    <div className={styles.container} data-testid="question-queue">
      {questions.map(q => (
        <Card key={q.id} size="small">
          <CardHeader
            header={<Text weight="semibold">❓ {q.question}</Text>}
            description={<Text size={200}>{q.rationale}</Text>}
          />
          <div className={styles.meta}>
            <Badge appearance="filled" color={priorityColor[q.priority] ?? 'informative'} size="small">
              {q.priority}
            </Badge>
            <Badge appearance="outline" size="small">{q.type}</Badge>
            {q.targetSpeaker && (
              <Badge appearance="outline" color="brand" size="small">→ {q.targetSpeaker}</Badge>
            )}
          </div>
        </Card>
      ))}
      {questions.length === 0 && <Text italic>質問はまだありません</Text>}
    </div>
  );
};
