import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './ProtectedRoute';

type AuthState = {
  isAuthenticated: boolean;
  session: { username: string; roles: string[] } | null;
};

const useAuthMock = vi.fn();

vi.mock('../auth/AuthContext', () => ({
  useAuth: () => useAuthMock(),
}));

function renderRoute(state: AuthState, roles?: string[]) {
  useAuthMock.mockReturnValue(state);

  render(
    <MemoryRouter initialEntries={['/secret']}>
      <Routes>
        <Route path="/auth/login" element={<div>login page</div>} />
        <Route path="/petitions" element={<div>petitions page</div>} />
        <Route element={<ProtectedRoute roles={roles} />}>
          <Route path="/secret" element={<div>secret page</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe('ProtectedRoute', () => {
  it('redirects anonymous users to login', async () => {
    renderRoute({ isAuthenticated: false, session: null });

    expect(await screen.findByText('login page')).toBeInTheDocument();
  });

  it('keeps authenticated users on the requested route', async () => {
    renderRoute({
      isAuthenticated: true,
      session: { username: 'alice', roles: ['User'] },
    });

    expect(await screen.findByText('secret page')).toBeInTheDocument();
  });

  it('redirects users without the required role to petitions', async () => {
    renderRoute(
      {
        isAuthenticated: true,
        session: { username: 'alice', roles: ['User'] },
      },
      ['Admin'],
    );

    expect(await screen.findByText('petitions page')).toBeInTheDocument();
  });
});