import { Routes, Route } from 'react-router-dom';
import {
  makeStyles, tokens,
  TabList, Tab,
} from '@fluentui/react-components';
import {
  BoardRegular, SettingsRegular, BookRegular, PeopleRegular,
} from '@fluentui/react-icons';
import { useNavigate, useLocation } from 'react-router-dom';
import { Dashboard } from './pages/Dashboard';
import { AgentSettingsPage } from './pages/AgentSettings';
import { KnowledgeManagement } from './pages/KnowledgeManagement';
import { UserManagement } from './pages/UserManagement';

const useStyles = makeStyles({
  root: {
    display: 'flex',
    flexDirection: 'column',
    height: '100vh',
  },
  nav: {
    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    padding: '0 16px',
  },
  content: {
    flex: 1,
    padding: '16px',
    overflow: 'auto',
  },
});

export function App() {
  const styles = useStyles();
  const navigate = useNavigate();
  const location = useLocation();

  const tabValue = location.pathname === '/' ? 'dashboard'
    : location.pathname.replace('/', '');

  return (
    <div className={styles.root}>
      <nav className={styles.nav}>
        <TabList
          selectedValue={tabValue}
          onTabSelect={(_, data) => {
            const path = data.value === 'dashboard' ? '/' : `/${data.value}`;
            navigate(path);
          }}
        >
          <Tab data-testid="nav-dashboard" value="dashboard" icon={<BoardRegular />}>Dashboard</Tab>
          <Tab data-testid="nav-settings" value="settings" icon={<SettingsRegular />}>Settings</Tab>
          <Tab data-testid="nav-knowledge" value="knowledge" icon={<BookRegular />}>Knowledge</Tab>
          <Tab data-testid="nav-users" value="users" icon={<PeopleRegular />}>Users</Tab>
        </TabList>
      </nav>
      <main className={styles.content}>
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/settings" element={<AgentSettingsPage />} />
          <Route path="/knowledge" element={<KnowledgeManagement />} />
          <Route path="/users" element={<UserManagement />} />
        </Routes>
      </main>
    </div>
  );
}
