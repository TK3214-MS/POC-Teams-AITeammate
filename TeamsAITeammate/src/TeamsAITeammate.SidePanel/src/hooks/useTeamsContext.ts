import { useState, useEffect } from 'react';
import { app, meeting } from '@microsoft/teams-js';

interface TeamsContext {
  meetingId: string | null;
  userId: string | null;
  tenantId: string | null;
  isInMeeting: boolean;
  initialized: boolean;
}

export function useTeamsContext(): TeamsContext {
  const [context, setContext] = useState<TeamsContext>({
    meetingId: null,
    userId: null,
    tenantId: null,
    isInMeeting: false,
    initialized: false,
  });

  useEffect(() => {
    const init = async () => {
      try {
        await app.initialize();
        const ctx = await app.getContext();
        setContext({
          meetingId: ctx.meeting?.id ?? null,
          userId: ctx.user?.id ?? null,
          tenantId: ctx.user?.tenant?.id ?? null,
          isInMeeting: !!ctx.meeting?.id,
          initialized: true,
        });
      } catch {
        // Not running inside Teams — use development defaults
        console.warn('Teams SDK not available, using dev defaults');
        setContext({
          meetingId: 'dev-meeting-id',
          userId: 'dev-user-id',
          tenantId: 'dev-tenant-id',
          isInMeeting: true,
          initialized: true,
        });
      }
    };

    init();

    return () => {
      // No explicit cleanup needed for app.initialize
    };
  }, []);

  return context;
}

// Re-export meeting namespace for sharing to stage
export { meeting };
