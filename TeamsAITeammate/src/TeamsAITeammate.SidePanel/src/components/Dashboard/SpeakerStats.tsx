import React from 'react';
import {
  Text,
  makeStyles,
  tokens,
  ProgressBar,
} from '@fluentui/react-components';
import type { SpeakerStats as SpeakerStatsType } from '../../types';

const useStyles = makeStyles({
  container: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  row: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS },
  name: { minWidth: '100px' },
  bar: { flex: 1 },
});

interface Props {
  stats: SpeakerStatsType[];
}

export const SpeakerStats: React.FC<Props> = ({ stats }) => {
  const styles = useStyles();
  const maxSegments = Math.max(...stats.map(s => s.segmentCount), 1);

  return (
    <div className={styles.container}>
      {stats.map(speaker => (
        <div key={speaker.speakerId} className={styles.row}>
          <Text className={styles.name} size={200} weight="semibold">
            {speaker.speakerName}
          </Text>
          <ProgressBar
            className={styles.bar}
            value={speaker.segmentCount / maxSegments}
            thickness="large"
          />
          <Text size={200}>{speaker.segmentCount}</Text>
        </div>
      ))}
      {stats.length === 0 && <Text italic>話者データなし</Text>}
    </div>
  );
};
