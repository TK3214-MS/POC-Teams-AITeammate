import React, { useState } from 'react';
import {
  Text,
  Switch,
  SpinButton,
  Dropdown,
  Option,
  Button,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import type { AgentSettings as AgentSettingsType } from '../../types';

const useStyles = makeStyles({
  container: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM, padding: tokens.spacingVerticalS },
  field: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS },
  actions: { display: 'flex', gap: tokens.spacingHorizontalS, marginTop: tokens.spacingVerticalM },
});

interface Props {
  initialSettings: AgentSettingsType;
  onSave: (settings: AgentSettingsType) => void;
}

const frequencyOptions = [
  { label: '低（60秒）', value: 60 },
  { label: '中（30秒）', value: 30 },
  { label: '高（15秒）', value: 15 },
];

export const AgentSettings: React.FC<Props> = ({ initialSettings, onSave }) => {
  const styles = useStyles();
  const [settings, setSettings] = useState<AgentSettingsType>(initialSettings);

  return (
    <div className={styles.container} data-testid="agent-settings">
      <Text weight="semibold" size={500}>⚙️ エージェント設定</Text>

      <div className={styles.field}>
        <Text weight="semibold">介入頻度（沈黙検知閾値）</Text>
        <Dropdown
          value={frequencyOptions.find(o => o.value === settings.silenceThreshold)?.label ?? '中（30秒）'}
          onOptionSelect={(_e, data) => {
            const val = frequencyOptions.find(o => o.label === data.optionText)?.value ?? 30;
            setSettings(prev => ({ ...prev, silenceThreshold: val }));
          }}
        >
          {frequencyOptions.map(o => (
            <Option key={o.value} value={String(o.value)}>{o.label}</Option>
          ))}
        </Dropdown>
      </div>

      <div className={styles.field}>
        <Text weight="semibold">プロアクティブ介入</Text>
        <Switch
          checked={settings.enableProactiveIntervention}
          onChange={(_e, data) =>
            setSettings(prev => ({ ...prev, enableProactiveIntervention: data.checked }))
          }
          label={settings.enableProactiveIntervention ? '有効' : '無効'}
        />
      </div>

      <div className={styles.field}>
        <Text weight="semibold">最大介入回数（1会議あたり）</Text>
        <SpinButton
          value={settings.maxInterventionsPerMeeting}
          min={1}
          max={50}
          onChange={(_e, data) =>
            setSettings(prev => ({ ...prev, maxInterventionsPerMeeting: data.value ?? 20 }))
          }
        />
      </div>

      <div className={styles.actions}>
        <Button appearance="primary" onClick={() => onSave(settings)}>保存</Button>
        <Button appearance="secondary" onClick={() => setSettings(initialSettings)}>リセット</Button>
      </div>
    </div>
  );
};
