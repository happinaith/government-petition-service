import { Suspense, lazy } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { ROUTES } from "./routing/routes";
import "./App.css";

const LoginPage = lazy(async () => {
  const module = await import("./pages/LoginPage");
  return { default: module.LoginPage };
});

const PetitionsPage = lazy(async () => {
  const module = await import("./pages/PetitionsPage");
  return { default: module.PetitionsPage };
});

function App() {
  return (
    <Suspense fallback={<main><section className="card" aria-busy="true">Загрузка страницы...</section></main>}>
      <Routes>
        <Route path={ROUTES.AUTH_LOGIN} element={<LoginPage />} />
        <Route path="/login" element={<Navigate to={ROUTES.AUTH_LOGIN} replace />} />
        <Route element={<ProtectedRoute />}>
          <Route path={ROUTES.PETITIONS} element={<PetitionsPage />} />
          <Route path="/" element={<Navigate to={ROUTES.PETITIONS} replace />} />
        </Route>
        <Route path="*" element={<Navigate to={ROUTES.PETITIONS} replace />} />
      </Routes>
    </Suspense>
  );
}

export default App;