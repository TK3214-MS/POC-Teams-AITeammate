import React from 'react';
import {
  Card,
  CardHeader,
  Text,
  Badge,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { KnowledgeEntry } from '../../types';

const useStyles = makeStyles({
  container: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  tags: { display: 'flex', gap: tokens.spacingHorizontalXS, flexWrap: 'wrap', marginTop: tokens.spacingVerticalXS },
});

interface Props {
  entries: KnowledgeEntry[];
}

export const KnowledgeList: React.FC<Props> = ({ entries }) => {
  const styles = useStyles();

  return (
    <div className={styles.container} data-testid="knowledge-list">
      {entries.map(entry => (
        <Card key={entry.id} size="small">
          <CardHeader
            header={<Text weight="semibold">{entry.title}</Text>}
            description={<Text size={200}>{entry.content}</Text>}
          />
          <div className={styles.tags}>
            <Badge appearance="outline" color="informative" size="small">{entry.type}</Badge>
            {entry.tags.map(tag => (
              <Badge key={tag} appearance="outline" size="small">{tag}</Badge>
            ))}
          </div>
        </Card>
      ))}
      {entries.length === 0 && <Text italic>ナレッジはまだありません</Text>}
    </div>
  );
};
