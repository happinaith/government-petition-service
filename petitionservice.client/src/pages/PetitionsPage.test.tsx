import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthContext';
import { ROUTES } from '../routing/routes';
import { PetitionsPage } from './PetitionsPage';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function petitionsResponse(items: Array<Record<string, unknown>> = []) {
  return jsonResponse({
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 10,
    sortBy: 'createdAt',
    sortDir: 'desc',
  });
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={[ROUTES.PETITIONS]}>
      <AuthProvider>
        <Routes>
          <Route path={ROUTES.AUTH_LOGIN} element={<div>login page</div>} />
          <Route path={ROUTES.PETITIONS} element={<PetitionsPage />} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.unstubAllGlobals();
});

describe('PetitionsPage', () => {
  it('shows role-specific actions and a loaded list', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';

      if (url === '/api/auth/me' && method === 'GET') {
        return jsonResponse({ username: 'alice', roles: ['Admin'] });
      }

      if (url === '/api/petitions?' && method === 'GET') {
        return petitionsResponse([
          {
            id: 1,
            title: 'Save the park',
            category: 'Environment',
            createdAt: '2026-05-07T00:00:00Z',
            author: 'alice',
            signatures: 12,
          },
        ]);
      }

      throw new Error(`Unexpected request: ${method} ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    expect(await screen.findByText('Save the park')).toBeInTheDocument();
    expect(screen.getByText('Инструменты администратора')).toBeInTheDocument();
    expect(screen.getByText('Создать петицию')).toBeInTheDocument();
    expect(screen.getByText(/Вы вошли как/)).toHaveTextContent('alice');
  });

  it('applies filters through the query string', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';

      if (url === '/api/auth/me' && method === 'GET') {
        return jsonResponse({ username: 'alice', roles: ['User'] });
      }

      if (url === '/api/petitions?' && method === 'GET') {
        return petitionsResponse([
          {
            id: 1,
            title: 'Save the park',
            category: 'Environment',
            createdAt: '2026-05-07T00:00:00Z',
            author: 'alice',
            signatures: 12,
          },
        ]);
      }

      if (url.startsWith('/api/petitions?') && url !== '/api/petitions?' && method === 'GET') {
        return petitionsResponse([
          {
            id: 2,
            title: 'Protect the river',
            category: 'Water',
            createdAt: '2026-05-07T00:00:00Z',
            author: 'bob',
            signatures: 5,
          },
        ]);
      }

      throw new Error(`Unexpected request: ${method} ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    await screen.findByText('Save the park');
    await user.type(screen.getByPlaceholderText('Поиск по заголовку, тексту, автору'), 'river');
    await user.type(screen.getAllByPlaceholderText('Категория')[1], 'Water');
    await user.click(screen.getByRole('button', { name: 'Применить фильтры' }));

    await waitFor(() => {
      expect(
        fetchMock.mock.calls.some(([url]) => {
          const value = String(url);
          return value.includes('/api/petitions?') && value.includes('q=river') && value.includes('category=Water') && value.includes('page=1');
        }),
      ).toBe(true);
    });
  });

  it('surfaces server validation errors on create', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';

      if (url === '/api/auth/me' && method === 'GET') {
        return jsonResponse({ username: 'alice', roles: ['User'] });
      }

      if (url === '/api/petitions?' && method === 'GET') {
        return petitionsResponse([]);
      }

      if (url === '/api/petitions' && method === 'POST') {
        return jsonResponse({ detail: 'Петиция с таким заголовком уже существует.' }, 409);
      }

      throw new Error(`Unexpected request: ${method} ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);

    renderPage();

    await screen.findByPlaceholderText('Заголовок');
    await user.type(screen.getByPlaceholderText('Заголовок'), 'Better roads');
    await user.type(screen.getByPlaceholderText('Текст'), 'This is a long enough petition text.');
    await user.click(screen.getByRole('button', { name: 'Создать' }));

    expect(await screen.findAllByText('Петиция с таким заголовком уже существует.')).toHaveLength(2);
  });
});