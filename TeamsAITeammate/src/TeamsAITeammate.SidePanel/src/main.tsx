import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import Configure from './Configure';

const rootComponent = window.location.pathname === '/configure'
  ? <Configure />
  : <App />;

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {rootComponent}
  </StrictMode>,
);
