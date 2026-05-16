import { useEffect, useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { AuthProvider, useAuth } from './AuthContext';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function AuthProbe() {
  const { session, authFetch } = useAuth();
  const [statusText, setStatusText] = useState('waiting');

  useEffect(() => {
    void (async () => {
      const response = await authFetch('/api/petitions');
      setStatusText(`response:${response.status}`);
    })();
  }, [authFetch]);

  return (
    <div>
      <p>{session?.username ?? 'signed-out'}</p>
      <p>{statusText}</p>
    </div>
  );
}

describe('AuthProvider', () => {
  it('refreshes the session and retries protected requests after a 401', async () => {
    let petitionCallCount = 0;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';

      if (url === '/api/auth/me' && method === 'GET') {
        return jsonResponse({ username: 'alice', roles: ['User'] });
      }

      if (url === '/api/petitions' && method === 'GET') {
        petitionCallCount += 1;
        return petitionCallCount === 1
          ? new Response('', { status: 401 })
          : jsonResponse({ items: [], totalCount: 0, page: 1, pageSize: 10, sortBy: 'createdAt', sortDir: 'desc' });
      }

      if (url === '/api/auth/refresh' && method === 'POST') {
        return jsonResponse({ username: 'alice-renewed', roles: ['User'] });
      }

      throw new Error(`Unexpected request: ${method} ${url}`);
    });

    vi.stubGlobal('fetch', fetchMock);

    render(
      <MemoryRouter>
        <AuthProvider>
          <AuthProbe />
        </AuthProvider>
      </MemoryRouter>,
    );

    expect(await screen.findByText('alice-renewed')).toBeInTheDocument();
    expect(await screen.findByText('response:200')).toBeInTheDocument();
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/me', expect.any(Object));
    expect(fetchMock).toHaveBeenCalledWith('/api/petitions', expect.any(Object));
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/refresh', expect.any(Object));
  });

  it('clears the session when refresh fails during an expired request', async () => {
    let petitionCallCount = 0;
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';

      if (url === '/api/auth/me' && method === 'GET') {
        return new Response('', { status: 401 });
      }

      if (url === '/api/petitions' && method === 'GET') {
        petitionCallCount += 1;
        return new Response('', { status: 401 });
      }

      if (url === '/api/auth/refresh' && method === 'POST') {
        return new Response('', { status: 401 });
      }

      throw new Error(`Unexpected request: ${method} ${url}`);
    });

    vi.stubGlobal('fetch', fetchMock);

    render(
      <MemoryRouter>
        <AuthProvider>
          <AuthProbe />
        </AuthProvider>
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByText('response:401')).toBeInTheDocument();
    });

    await waitFor(() => {
      expect(screen.getByText('signed-out')).toBeInTheDocument();
    });
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith('/api/auth/refresh', expect.any(Object));
    });
  });
});