import { useState, type FormEvent, type ReactElement } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { ROUTES } from "../routing/routes";
import { SeoHead } from "../seo/SeoHead";

export function LoginPage(): ReactElement {
  const { isAuthenticated, login, register } = useAuth();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (isAuthenticated) {
    return <Navigate to={ROUTES.PETITIONS} replace />;
  }

  const submit = async (event: FormEvent<HTMLFormElement>, mode: "login" | "register"): Promise<void> => {
    event.preventDefault();
    setError(null);
    setBusy(true);

    try {
      if (mode === "login") {
        await login(username, password);
      } else {
        await register(username, password);
      }
    } catch {
      setError(mode === "login" ? "Неверный логин или пароль" : "Не удалось зарегистрироваться");
    } finally {
      setBusy(false);
    }
  };

  const registerWithCurrentValues = async (): Promise<void> => {
    setError(null);
    setBusy(true);

    try {
      await register(username, password);
    } catch {
      setError("Не удалось зарегистрироваться");
    } finally {
      setBusy(false);
    }
  };

  return (
    <main>
      <SeoHead
        title="Вход в сервис петиций"
        description="Авторизация и регистрация пользователей сервиса электронных петиций."
        canonicalPath={ROUTES.AUTH_LOGIN}
        robots="noindex, nofollow"
      />

      <section className="auth-card" aria-labelledby="auth-title">
        <header>
          <img src="/vite.svg" alt="Логотип сервиса петиций" width={48} height={48} />
          <h1 id="auth-title">Сервис петиций</h1>
          <h2>Вход и регистрация</h2>
          <p>Войдите, чтобы управлять петициями, подписывать инициативы и работать с вложениями.</p>
        </header>

        <section aria-labelledby="auth-form-title">
          <h3 id="auth-form-title">Доступ к личному кабинету</h3>
          <form onSubmit={(e) => void submit(e, "login")}>
            <label htmlFor="username">Логин</label>
            <input
              id="username"
              placeholder="Логин"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
              autoComplete="username"
            />

            <label htmlFor="password">Пароль</label>
            <input
              id="password"
              placeholder="Пароль"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              autoComplete="current-password"
            />

            <div className="actions">
              <button type="submit" disabled={busy}>
                Войти
              </button>
              <button type="button" disabled={busy} onClick={() => void registerWithCurrentValues()}>
                Регистрация
              </button>
            </div>
          </form>
          {error ? <p className="error">{error}</p> : null}
        </section>
      </section>
    </main>
  );
}
