import { useEffect, useState } from 'react';
import { Link, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import './App.css';
import { AuthPage } from './pages/AuthPage';
import { PetitionsPage } from './pages/PetitionsPage';
import { ProfilePage } from './pages/ProfilePage';
import { PetitionDetailsPage } from './pages/PetitionDetailsPage';
import { AdminPage } from './pages/AdminPage';

interface AuthResponse {
    username: string;
    isAdmin: boolean;
}

function App() {
    const [isAuthenticated, setIsAuthenticated] = useState(false);
    const [username, setUsername] = useState<string | null>(null);
    const [isAdmin, setIsAdmin] = useState(false);
    const navigate = useNavigate();
    const location = useLocation();

    function clearUserState() {
        setIsAuthenticated(false);
        setUsername(null);
        setIsAdmin(false);
    }

    function applyAuthSuccess(data: AuthResponse) {
        setIsAuthenticated(true);
        setUsername(data.username);
        setIsAdmin(data.isAdmin);
    }

    async function refreshAccessToken(): Promise<boolean> {
        try {
            const resp = await fetch('/api/auth/refresh', {
                method: 'POST'
            });

            if (!resp.ok) {
                clearUserState();
                return false;
            }

            const data: AuthResponse = await resp.json();
            applyAuthSuccess(data);
            return true;
        } catch {
            return false;
        }
    }

    async function loadCurrentUser() {
        const meResponse = await fetch('/api/auth/me');

        if (meResponse.ok) {
            const meData = await meResponse.json();
            if (meData?.username) {
                setIsAuthenticated(true);
                setUsername(meData.username);
                setIsAdmin(!!meData.isAdmin);
            }
            return;
        }

        if (meResponse.status !== 401) {
            clearUserState();
            return;
        }

        const refreshed = await refreshAccessToken();
        if (!refreshed) {
            clearUserState();
            return;
        }

        const retryResponse = await fetch('/api/auth/me');
        if (retryResponse.ok) {
            const retryData = await retryResponse.json();
            if (retryData?.username) {
                setIsAuthenticated(true);
                setUsername(retryData.username);
                setIsAdmin(!!retryData.isAdmin);
                return;
            }
        }

        clearUserState();
    }

    useEffect(() => {
        void loadCurrentUser();
    }, []);

    function handleAuthSuccess(data: AuthResponse) {
        applyAuthSuccess(data);
        navigate('/profile');
    }

    async function logout() {
        try {
            await fetch('/api/auth/logout', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ allDevices: false })
            });
        } catch {
            // Ignore network failures during logout and clear local state anyway.
        }

        clearUserState();
        navigate('/auth');
    }

    const isAuthPage = location.pathname.startsWith('/auth');

    return (
        <div className="app-root">
            {!isAuthPage && (
                <header className="app-header">
                    <div className="app-header-left">
                        <h1>Сервис петиций</h1>
                        <nav>
                            <Link to="/petitions">Петиции</Link>
                            {isAuthenticated && <Link to="/profile">Мой профиль</Link>}
                            {isAuthenticated && isAdmin && <Link to="/admin">Админ</Link>}
                        </nav>
                    </div>
                    <div className="app-header-right">
                        {isAuthenticated && (
                            <>
                                <span>Вы вошли как {username ?? 'Пользователь'}</span>
                                <button onClick={logout}>Выйти</button>
                            </>
                        )}
                        {!isAuthenticated && !isAuthPage && (
                            <Link to="/auth">Войти</Link>
                        )}
                    </div>
                </header>
            )}

            <main className="app-main">
                <Routes>
                    <Route path="/auth" element={<AuthPage onAuthSuccess={handleAuthSuccess} />} />
                    <Route path="/petitions" element={<PetitionsPage/>} />
                    <Route path="/petitions/:id" element={<PetitionDetailsPage />} />
                    <Route path="/profile" element={<ProfilePage username={username} />} />
                    <Route path="/admin" element={isAuthenticated && isAdmin ? <AdminPage /> : <AuthPage onAuthSuccess={handleAuthSuccess} />} />
                    <Route
                        path="/"
                        element={isAuthenticated ? <PetitionsPage/> : <AuthPage onAuthSuccess={handleAuthSuccess} />}
                    />
                </Routes>
            </main>
        </div>
    );
}

export default App;