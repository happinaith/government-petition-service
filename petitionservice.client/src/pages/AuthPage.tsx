import { useState } from 'react';

interface AuthResponse {
  username: string;
  isAdmin: boolean;
}

interface AuthPageProps {
  onAuthSuccess: (data: AuthResponse) => void;
}

export function AuthPage({ onAuthSuccess }: AuthPageProps) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [error, setError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  function validatePassword(pw: string): string | null {
    if (pw.length < 6) return 'Пароль должен быть не короче 6 символов';
    return null;
  }

  async function submit() {
    setError(null);
    setPasswordError(null);

    if (mode === 'register') {
      const pwdError = validatePassword(password);
      if (pwdError) {
        setPasswordError(pwdError);
        return;
      }
    }

    setLoading(true);
    try {
      const url = mode === 'login' ? '/api/auth/login' : '/api/auth/register';
      const resp = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password })
      });
      if (!resp.ok) {
        setError(mode === 'login' ? 'Неверный логин или пароль' : 'Ошибка при регистрации');
        return;
      }
      const data: AuthResponse = await resp.json();
      onAuthSuccess(data);
    } catch (e) {
      setError('Сетевая ошибка, попробуйте позже');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="auth-page">
      <div className="auth-card">
        <h1>Сервис петиций</h1>
        <div className="auth-toggle">
          <button
            className={mode === 'login' ? 'active' : ''}
            onClick={() => setMode('login')}
          >
            Вход
          </button>
          <button
            className={mode === 'register' ? 'active' : ''}
            onClick={() => setMode('register')}
          >
            Регистрация
          </button>
        </div>
        <div className="auth-form">
          <label>
            Имя пользователя
            <input
              value={username}
              onChange={e => setUsername(e.target.value)}
              placeholder="Введите имя пользователя"
            />
          </label>
          <label>
            Пароль
            <input
              type="password"
              value={password}
              onChange={e => {
                const v = e.target.value;
                setPassword(v);
                // Не пугаем пользователя во время печати при входе,
                // показываем ошибку только после попытки регистрации
                if (mode === 'register' && passwordError) {
                  setPasswordError(validatePassword(v));
                }
              }}
              placeholder="Введите пароль"
            />
          </label>
          {passwordError && mode === 'register' && (
            <p className="error-text">{passwordError}</p>
          )}
          {error && <p className="error-text">{error}</p>}
          <button
            onClick={submit}
            disabled={
              loading ||
              !username ||
              !password ||
              (mode === 'register' && !!passwordError)
            }
          >
            {loading ? 'Отправка...' : mode === 'login' ? 'Войти' : 'Зарегистрироваться'}
          </button>
        </div>
      </div>
    </div>
  );
}
