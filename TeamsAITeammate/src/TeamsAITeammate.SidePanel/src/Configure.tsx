import { useEffect, useState } from 'react';
import { app, pages } from '@microsoft/teams-js';
import {
  Body1,
  Button,
  FluentProvider,
  Spinner,
  Title2,
  makeStyles,
  teamsDarkTheme,
  teamsLightTheme,
  tokens,
} from '@fluentui/react-components';

const useStyles = makeStyles({
  root: {
    minHeight: '100vh',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: tokens.spacingHorizontalXXL,
  },
  content: {
    maxWidth: '480px',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    textAlign: 'center',
  },
});

export default function Configure() {
  const styles = useStyles();
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isDark = window.matchMedia('(prefers-color-scheme: dark)').matches;

  useEffect(() => {
    const initialize = async () => {
      try {
        await app.initialize();
        pages.config.registerOnSaveHandler(async (saveEvent) => {
          await pages.config.setConfig({
            entityId: 'ai-teammate-meeting-panel',
            suggestedDisplayName: 'AI Teammate',
            contentUrl: `${window.location.origin}/sidepanel`,
            websiteUrl: `${window.location.origin}/sidepanel`,
          });
          saveEvent.notifySuccess();
        });
        await pages.config.setValidityState(true);
        setReady(true);
      } catch (reason) {
        setError(reason instanceof Error ? reason.message : 'Teams initialization failed');
      }
    };

    void initialize();
  }, []);

  return (
    <FluentProvider theme={isDark ? teamsDarkTheme : teamsLightTheme}>
      <main className={styles.root}>
        <div className={styles.content}>
          <Title2>AI Teammate</Title2>
          {error ? (
            <Body1>Teams内でこの構成画面を開いてください。</Body1>
          ) : ready ? (
            <>
              <Body1>会議のサイドパネルにAI Teammateを追加できます。</Body1>
              <Button appearance="primary" disabled>保存ボタンで追加</Button>
            </>
          ) : (
            <Spinner label="Teamsに接続しています" />
          )}
        </div>
      </main>
    </FluentProvider>
  );
}