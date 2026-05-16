import { useCallback, useEffect, useMemo, useRef, useState, type PropsWithChildren, type ReactElement } from "react";
import { AuthContext } from "./authContext";
import type { AuthContextValue } from "./authContext";
import type { AuthResponse, AuthSession } from "./types";

function toSession(response: AuthResponse): AuthSession {
  return {
    username: response.username,
    roles: response.roles,
  };
}

export function AuthProvider({ children }: PropsWithChildren): ReactElement {
  const [session, setSession] = useState<AuthSession | null>(null);
  const sessionRef = useRef<AuthSession | null>(session);
  const [ready, setReady] = useState(false);

  const setSessionState = useCallback((nextSession: AuthSession | null) => {
    sessionRef.current = nextSession;
    setSession(nextSession);
  }, []);

  const clearSession = useCallback(() => {
    setSessionState(null);
  }, [setSessionState]);

  const refreshSession = useCallback(async (): Promise<boolean> => {
    const resp = await fetch("/api/auth/refresh", {
      method: "POST",
      credentials: "same-origin",
    });

    if (!resp.ok) {
      clearSession();
      return false;
    }

    const payload = (await resp.json()) as AuthResponse;
    setSessionState(toSession(payload));
    return true;
  }, [clearSession, setSessionState]);

  const syncSessionFromServer = useCallback(async (): Promise<void> => {
    const meResp = await fetch("/api/auth/me", {
      method: "GET",
      credentials: "same-origin",
    });

    if (meResp.ok) {
      const me = (await meResp.json()) as { username?: string; roles?: string[]; Username?: string; Roles?: string[] };
      const username = me.username ?? me.Username;
      const roles = me.roles ?? me.Roles ?? [];

      if (username) {
        setSessionState({ username, roles });
        return;
      }
    }

    const refreshed = await refreshSession();
    if (!refreshed) {
      clearSession();
    }
  }, [clearSession, refreshSession, setSessionState]);

  const authFetch = useCallback(
    async (input: RequestInfo | URL, init: RequestInit = {}, requireAuth = true): Promise<Response> => {
      if (!requireAuth) {
        return fetch(input, {
          ...init,
          credentials: "same-origin",
        });
      }

      const firstResponse = await fetch(input, {
        ...init,
        credentials: "same-origin",
      });

      if (firstResponse.status !== 401) {
        return firstResponse;
      }

      const refreshed = await refreshSession();
      if (!refreshed || !sessionRef.current) {
        clearSession();
        return firstResponse;
      }

      return fetch(input, {
        ...init,
        credentials: "same-origin",
      });
    },
    [clearSession, refreshSession],
  );

  useEffect(() => {
    void syncSessionFromServer().finally(() => {
      setReady(true);
    });

    const onFocus = () => {
      void syncSessionFromServer();
    };

    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") {
        void syncSessionFromServer();
      }
    };

    window.addEventListener("focus", onFocus);
    document.addEventListener("visibilitychange", onVisibilityChange);

    const intervalId = window.setInterval(() => {
      void syncSessionFromServer();
    }, 60000);

    return () => {
      window.removeEventListener("focus", onFocus);
      document.removeEventListener("visibilitychange", onVisibilityChange);
      window.clearInterval(intervalId);
    };
  }, [syncSessionFromServer]);

  const login = useCallback(async (username: string, password: string): Promise<void> => {
    const resp = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "same-origin",
      body: JSON.stringify({ username, password }),
    });

    if (!resp.ok) {
      throw new Error("Не удалось войти");
    }

    const payload = (await resp.json()) as AuthResponse;
    setSessionState(toSession(payload));
  }, [setSessionState]);

  const register = useCallback(async (username: string, password: string): Promise<void> => {
    const resp = await fetch("/api/auth/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "same-origin",
      body: JSON.stringify({ username, password }),
    });

    if (!resp.ok) {
      throw new Error("Не удалось зарегистрироваться");
    }

    const payload = (await resp.json()) as AuthResponse;
    setSessionState(toSession(payload));
  }, [setSessionState]);

  const logout = useCallback(async (): Promise<void> => {
    await fetch("/api/auth/logout", {
      method: "POST",
      credentials: "same-origin",
    });

    clearSession();
  }, [clearSession]);

  const hasRole = useCallback(
    (role: string): boolean => {
      const current = sessionRef.current;
      if (!current) {
        return false;
      }

      return current.roles.includes(role);
    },
    [],
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      session,
      isAuthenticated: ready && !!session,
      login,
      register,
      logout,
      hasRole,
      authFetch,
    }),
    [session, ready, login, register, logout, hasRole, authFetch],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}