import { useEffect, useState } from 'react';
import './App.css';

interface Petition {
    id: number;
    title: string;
    content: string;
    category?: string;
    createdAt: string;
    author: string;
    signatures: number;
}

interface AuthResponse { token: string; username: string; }

function App() {
    const [petitions, setPetitions] = useState<Petition[]>([]);
    const [loading, setLoading] = useState(true);
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [token, setToken] = useState<string | null>(() => localStorage.getItem('jwt'));
    const [newTitle, setNewTitle] = useState('');
    const [newContent, setNewContent] = useState('');
    const [error, setError] = useState<string | null>(null);

    useEffect(() => { fetchPetitions(); }, []);

    async function fetchPetitions() {
        setLoading(true);
        try {
            const resp = await fetch('/api/petitions');
            if (resp.ok) {
                const data = await resp.json();
                setPetitions(data);
            }
        } catch (e) {
            console.error(e);
        } finally { setLoading(false); }
    }

    async function register() {
        setError(null);
        const resp = await fetch('/api/auth/register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
        });
        if (!resp.ok) { setError('Регистрация не удалась'); return; }
        const data: AuthResponse = await resp.json();
        setToken(data.token); localStorage.setItem('jwt', data.token);
    }

    async function login() {
        setError(null);
        const resp = await fetch('/api/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
        });
        if (!resp.ok) { setError('Логин не удался'); return; }
        const data: AuthResponse = await resp.json();
        setToken(data.token); localStorage.setItem('jwt', data.token);
    }

    function logout() { setToken(null); localStorage.removeItem('jwt'); }

    async function createPetition() {
        if (!token) return;
        const resp = await fetch('/api/petitions', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
            body: JSON.stringify({ title: newTitle, content: newContent })
        });
        if (resp.ok) { setNewTitle(''); setNewContent(''); fetchPetitions(); }
    }

    async function sign(id: number) {
        const resp = await fetch(`/api/petitions/${id}/sign`, { method: 'POST' });
        if (resp.ok) fetchPetitions();
    }

    return (
        <div>
            <h1>Правительственные петиции</h1>
            {!token && (
                <div style={{ border: '1px solid #444', padding: '1rem', marginBottom: '1rem' }}>
                    <h2>Вход или регистрация</h2>
                    <input placeholder='Имя пользователя' value={username} onChange={e => setUsername(e.target.value)} />{' '}
                    <input placeholder='Пароль' type='password' value={password} onChange={e => setPassword(e.target.value)} />{' '}
                    <button onClick={login}>Войти</button>{' '}
                    <button onClick={register}>Зарегистрироваться</button>
                    {error && <p style={{ color: 'red' }}>{error}</p>}
                </div>
            )}
            {token && (
                <div style={{ marginBottom: '1rem' }}>
                    <span>Вы вошли как {username || 'пользователь'}</span>{' '}
                    <button onClick={logout}>Выйти</button>
                </div>
            )}
            {token && (
                <div style={{ border: '1px solid #444', padding: '1rem', marginBottom: '1rem' }}>
                    <h2>Создать петицию</h2>
                    <input placeholder='Заголовок' value={newTitle} onChange={e => setNewTitle(e.target.value)} />{' '}
                    <textarea placeholder='Текст' value={newContent} onChange={e => setNewContent(e.target.value)} />{' '}
                    <button onClick={createPetition}>Отправить</button>
                </div>
            )}
            <h2>Список петиций</h2>
            {loading ? <p>Загрузка...</p> : (
                <table>
                    <thead>
                        <tr>
                            <th>Заголовок</th>
                            <th>Автор</th>
                            <th>Категория</th>
                            <th>Подписей</th>
                            <th>Действия</th>
                        </tr>
                    </thead>
                    <tbody>
                        {petitions.map(p => (
                            <tr key={p.id}>
                                <td>{p.title}</td>
                                <td>{p.author}</td>
                                <td>{p.category || '-'}</td>
                                <td>{p.signatures}</td>
                                <td><button onClick={() => sign(p.id)}>Подписать</button></td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}

export default App;