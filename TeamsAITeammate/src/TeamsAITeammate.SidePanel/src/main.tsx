import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import Configure from './Configure';
import TranscriptCapture from './components/Capture/TranscriptCapture';

const rootComponent = window.location.pathname === '/configure'
  ? <Configure />
  : window.location.pathname === '/capture'
    ? <TranscriptCapture />
    : <App />;

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {rootComponent}
  </StrictMode>,
);
