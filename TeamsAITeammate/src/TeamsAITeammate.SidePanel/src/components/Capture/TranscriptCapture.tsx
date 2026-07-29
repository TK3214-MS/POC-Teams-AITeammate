import { useEffect, useRef, useState } from 'react';
import {
  Body1,
  Button,
  FluentProvider,
  MessageBar,
  MessageBarBody,
  Spinner,
  Subtitle1,
  makeStyles,
  teamsDarkTheme,
  teamsLightTheme,
  tokens,
} from '@fluentui/react-components';
import { Mic24Regular, Stop24Regular } from '@fluentui/react-icons';
import { app } from '@microsoft/teams-js';
import * as SpeechSDK from 'microsoft-cognitiveservices-speech-sdk';
import { getSpeechAuthorization, submitTranscriptSegment } from '../../services/transcriptApi';

const useStyles = makeStyles({
  root: {
    minHeight: '100vh',
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingVerticalXL,
  },
  actions: { display: 'flex', gap: tokens.spacingHorizontalS },
  transcript: {
    minHeight: '120px',
    padding: tokens.spacingVerticalM,
    borderTop: `1px solid ${tokens.colorNeutralStroke1}`,
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    whiteSpace: 'pre-wrap',
  },
});

type CaptureState = 'initializing' | 'ready' | 'listening' | 'stopping' | 'error';

export default function TranscriptCapture() {
  const styles = useStyles();
  const recognizerRef = useRef<SpeechSDK.SpeechRecognizer | null>(null);
  const refreshTimerRef = useRef<number | null>(null);
  const [state, setState] = useState<CaptureState>('initializing');
  const [latestText, setLatestText] = useState('');
  const [error, setError] = useState<string | null>(null);
  const isDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  const meetingId = new URLSearchParams(window.location.search).get('meetingId');

  const clearRefreshTimer = () => {
    if (refreshTimerRef.current !== null) {
      window.clearTimeout(refreshTimerRef.current);
      refreshTimerRef.current = null;
    }
  };

  const closeRecognizer = () => {
    clearRefreshTimer();
    recognizerRef.current?.close();
    recognizerRef.current = null;
  };

  useEffect(() => {
    const initialize = async () => {
      try {
        await app.initialize();
        if (!meetingId) throw new Error('会議コンテキストを取得できません。');
        setState('ready');
      } catch (reason) {
        setError(reason instanceof Error ? reason.message : 'Teamsの初期化に失敗しました。');
        setState('error');
      }
    };
    void initialize();
    return closeRecognizer;
  }, [meetingId]);

  const scheduleTokenRefresh = (recognizer: SpeechSDK.SpeechRecognizer) => {
    clearRefreshTimer();
    refreshTimerRef.current = window.setTimeout(async () => {
      try {
        const authorization = await getSpeechAuthorization();
        recognizer.authorizationToken = authorization.token;
        scheduleTokenRefresh(recognizer);
      } catch {
        setError('Speech認証の更新に失敗しました。録音を再開してください。');
        stopCapture();
      }
    }, 8 * 60 * 1000);
  };

  const startCapture = async () => {
    if (!meetingId) return;
    setError(null);
    try {
      const permissionStream = await navigator.mediaDevices.getUserMedia({ audio: true });
      permissionStream.getTracks().forEach(track => track.stop());

      const authorization = await getSpeechAuthorization();
      const speechConfig = SpeechSDK.SpeechConfig.fromAuthorizationToken(
        authorization.token,
        authorization.region,
      );
      speechConfig.speechRecognitionLanguage = 'ja-JP';
      const audioConfig = SpeechSDK.AudioConfig.fromDefaultMicrophoneInput();
      const recognizer = new SpeechSDK.SpeechRecognizer(speechConfig, audioConfig);

      recognizer.recognized = (_sender, event) => {
        if (event.result.reason !== SpeechSDK.ResultReason.RecognizedSpeech) return;
        const text = event.result.text.trim();
        if (!text) return;

        setLatestText(text);
        void submitTranscriptSegment({
          id: event.result.resultId || crypto.randomUUID(),
          meetingId,
          text,
          language: 'ja-JP',
          timestamp: new Date().toISOString(),
          durationMs: Number(event.result.duration) / 10_000,
          confidence: 1,
        }).catch(reason => {
          setError(reason instanceof Error ? reason.message : '発話の送信に失敗しました。');
        });
      };
      recognizer.canceled = (_sender, event) => {
        if (event.reason === SpeechSDK.CancellationReason.Error) {
          setError(event.errorDetails || '音声認識が中断されました。');
          setState('error');
        }
        closeRecognizer();
      };
      recognizer.sessionStopped = () => {
        closeRecognizer();
        setState('ready');
      };

      recognizerRef.current = recognizer;
      recognizer.startContinuousRecognitionAsync(
        () => {
          setState('listening');
          scheduleTokenRefresh(recognizer);
        },
        reason => {
          closeRecognizer();
          setError(reason);
          setState('error');
        },
      );
    } catch (reason) {
      closeRecognizer();
      setError(reason instanceof Error ? reason.message : 'マイクを開始できませんでした。');
      setState('error');
    }
  };

  const stopCapture = () => {
    const recognizer = recognizerRef.current;
    if (!recognizer) {
      setState('ready');
      return;
    }
    setState('stopping');
    clearRefreshTimer();
    recognizer.stopContinuousRecognitionAsync(
      () => {
        closeRecognizer();
        setState('ready');
      },
      () => {
        closeRecognizer();
        setState('ready');
      },
    );
  };

  return (
    <FluentProvider theme={isDark ? teamsDarkTheme : teamsLightTheme}>
      <main className={styles.root}>
        <Subtitle1>リアルタイム音声分析</Subtitle1>
        <Body1>この端末のマイク音声を文字に変換します。音声データ自体は保存しません。</Body1>

        {error && (
          <MessageBar intent="error"><MessageBarBody>{error}</MessageBarBody></MessageBar>
        )}

        <div className={styles.actions}>
          <Button
            appearance="primary"
            icon={<Mic24Regular />}
            disabled={state === 'initializing' || state === 'listening' || state === 'stopping'}
            onClick={() => void startCapture()}
          >
            分析開始
          </Button>
          <Button
            icon={<Stop24Regular />}
            disabled={state !== 'listening'}
            onClick={stopCapture}
          >
            停止
          </Button>
        </div>

        {(state === 'initializing' || state === 'stopping') && <Spinner size="small" />}
        {state === 'listening' && <Body1>マイク入力を分析中</Body1>}

        <div className={styles.transcript} aria-live="polite">
          {latestText || '認識した発話がここに表示されます。'}
        </div>
      </main>
    </FluentProvider>
  );
}