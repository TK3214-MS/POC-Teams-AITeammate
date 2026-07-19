import { useEffect, useState } from 'react';
import {
  Card, CardHeader, Title2, Body1, Spinner,
  makeStyles, tokens,
} from '@fluentui/react-components';
import {
  BookRegular, VideoRegular, BrainCircuitRegular, PeopleRegular,
} from '@fluentui/react-icons';
import { api } from '../api';
import type { DashboardStats } from '../types';

const useStyles = makeStyles({
  grid: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
    gap: '16px',
    marginBottom: '24px',
  },
  statCard: {
    padding: '16px',
  },
  statValue: {
    fontSize: '32px',
    fontWeight: 'bold',
    color: tokens.colorBrandForeground1,
  },
  categoryList: {
    display: 'flex',
    flexDirection: 'column',
    gap: '8px',
  },
  categoryRow: {
    display: 'flex',
    justifyContent: 'space-between',
    padding: '4px 0',
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
});

export function Dashboard() {
  const styles = useStyles();
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getDashboard()
      .then(setStats)
      .catch(() => setStats(null))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <Spinner label="読み込み中..." />;

  return (
    <div data-testid="admin-dashboard">
      <Title2>ダッシュボード</Title2>

      <div className={styles.grid}>
        <Card data-testid="stat-card" className={styles.statCard}>
          <CardHeader
            image={<BookRegular />}
            header={<Body1>ナレッジ総数</Body1>}
          />
          <div className={styles.statValue}>{stats?.totalKnowledgeEntries ?? 0}</div>
        </Card>

        <Card data-testid="stat-card" className={styles.statCard}>
          <CardHeader
            image={<VideoRegular />}
            header={<Body1>会議セッション</Body1>}
          />
          <div className={styles.statValue}>{stats?.totalMeetingSessions ?? 0}</div>
        </Card>

        <Card data-testid="stat-card" className={styles.statCard}>
          <CardHeader
            image={<BrainCircuitRegular />}
            header={<Body1>AI分析回数</Body1>}
          />
          <div className={styles.statValue}>{stats?.totalAnalysisExecutions ?? 0}</div>
        </Card>

        <Card data-testid="stat-card" className={styles.statCard}>
          <CardHeader
            image={<PeopleRegular />}
            header={<Body1>アクティブユーザー</Body1>}
          />
          <div className={styles.statValue}>{stats?.activeUsers ?? 0}</div>
        </Card>
      </div>

      {stats?.knowledgeByCategory && Object.keys(stats.knowledgeByCategory).length > 0 && (
        <Card>
          <CardHeader header={<Body1>カテゴリ別ナレッジ分布</Body1>} />
          <div className={styles.categoryList}>
            {Object.entries(stats.knowledgeByCategory).map(([category, count]) => (
              <div key={category} className={styles.categoryRow}>
                <span>{category}</span>
                <span>{count}</span>
              </div>
            ))}
          </div>
        </Card>
      )}

      {stats?.aiCost && (
        <Card style={{ marginTop: '16px' }}>
          <CardHeader header={<Body1>AI利用コスト</Body1>} />
          <div className={styles.categoryList}>
            <div className={styles.categoryRow}>
              <span>プロンプトトークン</span>
              <span>{stats.aiCost.totalPromptTokens.toLocaleString()}</span>
            </div>
            <div className={styles.categoryRow}>
              <span>生成トークン</span>
              <span>{stats.aiCost.totalCompletionTokens.toLocaleString()}</span>
            </div>
            <div className={styles.categoryRow}>
              <span>推定コスト (USD)</span>
              <span>${stats.aiCost.estimatedCostUsd.toFixed(2)}</span>
            </div>
          </div>
        </Card>
      )}
    </div>
  );
}
