
import { Navigate } from 'react-router-dom';
import type { ReactNode } from 'react';
import type { AuthUser } from '../features/auth/services/authService';

interface ProtectedRouteProps {
  user: AuthUser | null;
  allowedRoles?: string[];
  children: ReactNode;
}

export default function ProtectedRoute({
  user,
  allowedRoles,
  children,
}: ProtectedRouteProps) {
  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (
    allowedRoles &&
    !user.roles.some((role) => allowedRoles.includes(role))
  ) {
    return <Navigate to="/acceso-denegado" replace />;
  }

  return <>{children}</>;
}
