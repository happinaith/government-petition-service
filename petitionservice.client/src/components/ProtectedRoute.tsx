import type { ReactElement } from "react";
import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { ROUTES } from "../routing/routes";

interface ProtectedRouteProps {
  roles?: string[];
}

export function ProtectedRoute({ roles }: ProtectedRouteProps): ReactElement {
  const { isAuthenticated, session } = useAuth();

  if (!isAuthenticated || !session) {
    return <Navigate to={ROUTES.AUTH_LOGIN} replace />;
  }

  if (roles && roles.length > 0) {
    const hasAnyRole = roles.some((role) => session.roles.includes(role));
    if (!hasAnyRole) {
      return <Navigate to={ROUTES.PETITIONS} replace />;
    }
  }

  return <Outlet />;
}
