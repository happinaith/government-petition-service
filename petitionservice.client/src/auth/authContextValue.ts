import { createContext } from "react";
import type { AuthSession } from "./types";

export interface AuthContextValue {
  session: AuthSession | null;
  isAuthenticated: boolean;
  login: (username: string, password: string) => Promise<void>;
  register: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  hasRole: (role: string) => boolean;
  authFetch: (input: RequestInfo | URL, init?: RequestInit, requireAuth?: boolean) => Promise<Response>;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);