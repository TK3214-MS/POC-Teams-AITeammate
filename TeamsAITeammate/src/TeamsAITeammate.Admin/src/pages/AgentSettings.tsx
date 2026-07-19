import { useEffect, useState } from 'react';
import {
  Button, Card, CardHeader, Title2, Body1, Spinner,
  Select, Input, Switch, Field, Slider,
  makeStyles, tokens, MessageBar, MessageBarBody,
} from '@fluentui/react-components';
import { SaveRegular } from '@fluentui/react-icons';
import { api } from '../api';
import type { AgentSettings } from '../types';

const useStyles = makeStyles({
  form: { display: 'flex', flexDirection: 'column', gap: '16px', maxWidth: '600px' },
  section: { padding: '16px' },
  actions: { display: 'flex', gap: '8px', marginTop: '16px' },
});

export function AgentSettingsPage() {
  const styles = useStyles();
  const [settings, setSettings] = useState<AgentSettings | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    api.getSettings()
      .then(setSettings)
      .finally(() => setLoading(false));
  }, []);

  const save = async () => {
    if (!settings) return;
    setSaving(true);
    try {
      const updated = await api.updateSettings(settings);
      setSettings(updated);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <Spinner label="読み込み中..." />;
  if (!settings) return <Body1>設定の読み込みに失敗しました</Body1>;

  return (
    <div>
      <Title2>エージェント設定</Title2>

      {saved && (
        <MessageBar intent="success" style={{ marginBottom: '16px' }}>
          <MessageBarBody>設定を保存しました</MessageBarBody>
        </MessageBar>
      )}

      <div className={styles.form}>
        <Card className={styles.section}>
          <CardHeader header={<Body1>介入設定</Body1>} />
          <Field label="介入頻度">
            <Select
              data-testid="intervention-frequency"
              value={settings.intervention.frequency}
              onChange={(_, data) => setSettings({
                ...settings,
                intervention: { ...settings.intervention, frequency: data.value as 'low' | 'medium' | 'high' },
              })}
            >
              <option value="low">低</option>
              <option value="medium">中</option>
              <option value="high">高</option>
            </Select>
          </Field>
          <Field label={`沈黙検知閾値: ${settings.intervention.silenceThresholdSeconds}秒`}>
            <Slider
              min={5} max={60}
              value={settings.intervention.silenceThresholdSeconds}
              onChange={(_, data) => setSettings({
                ...settings,
                intervention: { ...settings.intervention, silenceThresholdSeconds: data.value },
              })}
            />
          </Field>
          <Field label={`最大介入回数/会議: ${settings.intervention.maxInterventionsPerMeeting}`}>
            <Slider
              min={1} max={50}
              value={settings.intervention.maxInterventionsPerMeeting}
              onChange={(_, data) => setSettings({
                ...settings,
                intervention: { ...settings.intervention, maxInterventionsPerMeeting: data.value },
              })}
            />
          </Field>
          <Field label="プロアクティブ介入">
            <Switch
              checked={settings.intervention.enableProactiveIntervention}
              onChange={(_, data) => setSettings({
                ...settings,
                intervention: { ...settings.intervention, enableProactiveIntervention: data.checked },
              })}
            />
          </Field>
        </Card>

        <Card className={styles.section}>
          <CardHeader header={<Body1>データストア設定</Body1>} />
          <Field label="プロバイダー">
            <Select
              value={settings.dataStore.primaryProvider}
              onChange={(_, data) => setSettings({
                ...settings,
                dataStore: { ...settings.dataStore, primaryProvider: data.value as AgentSettings['dataStore']['primaryProvider'] },
              })}
            >
              <option value="CosmosDB">Cosmos DB</option>
              <option value="Dataverse">Dataverse</option>
              <option value="AzureAISearch">Azure AI Search</option>
              <option value="SharePoint">SharePoint</option>
            </Select>
          </Field>
          <Field label="RAG検索">
            <Switch
              checked={settings.dataStore.enableRAG}
              onChange={(_, data) => setSettings({
                ...settings,
                dataStore: { ...settings.dataStore, enableRAG: data.checked },
              })}
            />
          </Field>
        </Card>

        <Card className={styles.section}>
          <CardHeader header={<Body1>言語設定</Body1>} />
          <Field label="自動検出">
            <Switch
              checked={settings.language.autoDetect}
              onChange={(_, data) => setSettings({
                ...settings,
                language: { ...settings.language, autoDetect: data.checked },
              })}
            />
          </Field>
          <Field label="優先言語">
            <Select
              value={settings.language.preferredLanguage}
              onChange={(_, data) => setSettings({
                ...settings,
                language: { ...settings.language, preferredLanguage: data.value },
              })}
            >
              <option value="ja-JP">日本語</option>
              <option value="en-US">English</option>
            </Select>
          </Field>
        </Card>

        <Card className={styles.section}>
          <CardHeader header={<Body1>会議フィルター</Body1>} />
          <Field label="全会議を対象">
            <Switch
              checked={settings.meetingFilter.includeAllMeetings}
              onChange={(_, data) => setSettings({
                ...settings,
                meetingFilter: { ...settings.meetingFilter, includeAllMeetings: data.checked },
              })}
            />
          </Field>
          <Field label={`最小参加者数: ${settings.meetingFilter.minimumParticipants}`}>
            <Slider
              min={2} max={20}
              value={settings.meetingFilter.minimumParticipants}
              onChange={(_, data) => setSettings({
                ...settings,
                meetingFilter: { ...settings.meetingFilter, minimumParticipants: data.value },
              })}
            />
          </Field>
        </Card>

        <div className={styles.actions}>
          <Button
            data-testid="save-settings"
            appearance="primary"
            icon={<SaveRegular />}
            onClick={save}
            disabled={saving}
          >
            {saving ? '保存中...' : '設定を保存'}
          </Button>
        </div>
      </div>
    </div>
  );
}
