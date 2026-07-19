import { useEffect, useState } from 'react';
import {
  Title2, Spinner, Badge, Select,
  Table, TableHeader, TableRow, TableHeaderCell, TableBody, TableCell,
} from '@fluentui/react-components';
import { api } from '../api';
import type { TenantUser } from '../types';

const roleColor: Record<string, 'brand' | 'success' | 'informative'> = {
  Admin: 'brand',
  User: 'success',
  Viewer: 'informative',
};

export function UserManagement() {
  const [users, setUsers] = useState<TenantUser[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.getUsers()
      .then(setUsers)
      .finally(() => setLoading(false));
  }, []);

  const handleRoleChange = async (userId: string, role: string) => {
    const updated = await api.updateUserRole(userId, role);
    setUsers(prev => prev.map(u => u.userId === userId ? updated : u));
  };

  if (loading) return <Spinner label="読み込み中..." />;

  return (
    <div>
      <Title2>ユーザー管理</Title2>

      <Table data-testid="user-table">
        <TableHeader>
          <TableRow>
            <TableHeaderCell>表示名</TableHeaderCell>
            <TableHeaderCell>メール</TableHeaderCell>
            <TableHeaderCell>権限</TableHeaderCell>
            <TableHeaderCell>参加会議数</TableHeaderCell>
            <TableHeaderCell>ナレッジ貢献数</TableHeaderCell>
            <TableHeaderCell>最終アクティブ</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {users.map(user => (
            <TableRow key={user.userId}>
              <TableCell>{user.displayName}</TableCell>
              <TableCell>{user.email}</TableCell>
              <TableCell>
                <Select
                  value={user.role}
                  onChange={(_, data) => handleRoleChange(user.userId, data.value)}
                >
                  <option value="Admin">管理者</option>
                  <option value="User">ユーザー</option>
                  <option value="Viewer">閲覧者</option>
                </Select>
              </TableCell>
              <TableCell>{user.stats.meetingsAttended}</TableCell>
              <TableCell>{user.stats.knowledgeContributed}</TableCell>
              <TableCell>
                {user.stats.lastActiveAt
                  ? new Date(user.stats.lastActiveAt).toLocaleDateString('ja-JP')
                  : '-'}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
