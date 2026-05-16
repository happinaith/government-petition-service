import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { LoginPage } from './LoginPage';

const login = vi.fn();
const register = vi.fn();

let authState = {
  isAuthenticated: false,
  session: null as { username: string; roles: string[] } | null,
  login,
  register,
};

vi.mock('../auth/AuthContext', () => ({
  useAuth: () => authState,
}));

function renderLoginPage() {
  return render(
    <MemoryRouter initialEntries={['/auth/login']}>
      <Routes>
        <Route path="/auth/login" element={<LoginPage />} />
        <Route path="/petitions" element={<div>petitions page</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  login.mockReset();
  register.mockReset();
  authState = {
    isAuthenticated: false,
    session: null,
    login,
    register,
  };
});

describe('LoginPage', () => {
  it('submits login credentials from the form', async () => {
    const user = userEvent.setup();
    login.mockResolvedValueOnce(undefined);
    renderLoginPage();

    await user.type(screen.getByLabelText('Логин'), 'alice');
    await user.type(screen.getByLabelText('Пароль'), 'secret123');
    await user.click(screen.getByRole('button', { name: 'Войти' }));

    expect(login).toHaveBeenCalledWith('alice', 'secret123');
  });

  it('shows a login error when authentication fails', async () => {
    const user = userEvent.setup();
    login.mockRejectedValueOnce(new Error('401'));
    renderLoginPage();

    await user.type(screen.getByLabelText('Логин'), 'alice');
    await user.type(screen.getByLabelText('Пароль'), 'wrong');
    await user.click(screen.getByRole('button', { name: 'Войти' }));

    expect(await screen.findByText('Неверный логин или пароль')).toBeInTheDocument();
  });

  it('starts registration from the same credentials', async () => {
    const user = userEvent.setup();
    register.mockResolvedValueOnce(undefined);
    renderLoginPage();

    await user.type(screen.getByLabelText('Логин'), 'bob');
    await user.type(screen.getByLabelText('Пароль'), 'secret123');
    await user.click(screen.getByRole('button', { name: 'Регистрация' }));

    expect(register).toHaveBeenCalledWith('bob', 'secret123');
  });
});