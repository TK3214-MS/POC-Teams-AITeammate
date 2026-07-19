import { useEffect, useState, useCallback } from 'react';
import {
  Button, Title2, Body1, Spinner, Input, Badge,
  Table, TableHeader, TableRow, TableHeaderCell, TableBody, TableCell,
  makeStyles, tokens, Dialog, DialogTrigger, DialogSurface,
  DialogTitle, DialogBody, DialogActions, DialogContent,
} from '@fluentui/react-components';
import { SearchRegular, DeleteRegular, AddRegular } from '@fluentui/react-icons';
import { api } from '../api';
import type { KnowledgeEntry } from '../types';

const useStyles = makeStyles({
  toolbar: {
    display: 'flex', gap: '8px', marginBottom: '16px', alignItems: 'center',
  },
  searchInput: { flex: 1, maxWidth: '400px' },
});

const statusColor: Record<string, 'brand' | 'success' | 'warning' | 'danger' | 'informative'> = {
  Draft: 'informative',
  Confirmed: 'success',
  Edited: 'warning',
  Rejected: 'danger',
  Archived: 'informative',
};

export function KnowledgeManagement() {
  const styles = useStyles();
  const [entries, setEntries] = useState<KnowledgeEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState('');

  const load = useCallback(async (searchQuery?: string) => {
    setLoading(true);
    try {
      const data = await api.getKnowledge(searchQuery);
      setEntries(data);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const handleSearch = () => load(query || undefined);

  const handleDelete = async (id: string) => {
    await api.deleteKnowledge(id);
    setEntries(prev => prev.filter(e => e.id !== id));
  };

  return (
    <div>
      <Title2>ナレッジ管理</Title2>

      <div className={styles.toolbar}>
        <Input
          data-testid="knowledge-search"
          className={styles.searchInput}
          placeholder="ナレッジを検索..."
          value={query}
          onChange={(_, data) => setQuery(data.value)}
          onKeyDown={e => e.key === 'Enter' && handleSearch()}
          contentBefore={<SearchRegular />}
        />
        <Button appearance="primary" onClick={handleSearch}>検索</Button>
        <Button icon={<AddRegular />} onClick={() => {/* TODO: open create dialog */}}>
          手動追加
        </Button>
      </div>

      {loading ? <Spinner label="読み込み中..." /> : (
        <Table data-testid="knowledge-table">
          <TableHeader>
            <TableRow>
              <TableHeaderCell>タイトル</TableHeaderCell>
              <TableHeaderCell>カテゴリ</TableHeaderCell>
              <TableHeaderCell>ステータス</TableHeaderCell>
              <TableHeaderCell>信頼度</TableHeaderCell>
              <TableHeaderCell>作成日</TableHeaderCell>
              <TableHeaderCell>操作</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {entries.map(entry => (
              <TableRow key={entry.id} data-testid="knowledge-row">
                <TableCell>{entry.title}</TableCell>
                <TableCell>{entry.category}</TableCell>
                <TableCell>
                  <Badge color={statusColor[entry.status] ?? 'informative'}>{entry.status}</Badge>
                </TableCell>
                <TableCell>{(entry.confidenceScore * 100).toFixed(0)}%</TableCell>
                <TableCell>{new Date(entry.createdAt).toLocaleDateString('ja-JP')}</TableCell>
                <TableCell>
                  <Dialog>
                    <DialogTrigger>
                      <Button size="small" icon={<DeleteRegular />} appearance="subtle" />
                    </DialogTrigger>
                    <DialogSurface>
                      <DialogBody>
                        <DialogTitle>削除確認</DialogTitle>
                        <DialogContent>
                          <Body1>「{entry.title}」を削除しますか？</Body1>
                        </DialogContent>
                        <DialogActions>
                          <DialogTrigger><Button appearance="secondary">キャンセル</Button></DialogTrigger>
                          <Button appearance="primary" onClick={() => handleDelete(entry.id)}>
                            削除
                          </Button>
                        </DialogActions>
                      </DialogBody>
                    </DialogSurface>
                  </Dialog>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}
