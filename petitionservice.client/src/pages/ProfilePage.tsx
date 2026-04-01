import { useEffect, useMemo, useState } from 'react';
import { predictCategory } from '../ai/categoryModel';

interface Petition {
  id: number;
  title: string;
  content: string;
  category?: string;
  createdAt: string;
  author: string;
  signatures: number;
}

interface ProfilePageProps {
  username: string | null;
}

export function ProfilePage({ username }: ProfilePageProps) {
  const [petitions, setPetitions] = useState<Petition[]>([]);
  const [loading, setLoading] = useState(true);
  const [newTitle, setNewTitle] = useState('');
  const [newContent, setNewContent] = useState('');
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  useEffect(() => {
    fetchPetitions();
  }, []);

  async function fetchPetitions() {
    setLoading(true);
    try {
      const resp = await fetch('/api/petitions');
      if (resp.ok) {
        const data = await resp.json();
        setPetitions(data);
      }
    } finally {
      setLoading(false);
    }
  }

  const myPetitions = useMemo(
    () => petitions.filter(p => p.author === (username ?? '')),
    [petitions, username]
  );

  async function createPetition() {
    if (!newContent.trim()) {
      setCreateError('Текст петиции не должен быть пустым');
      return;
    }

    setCreateError(null);
    setCreating(true);
    try {
      const text = `${newTitle} ${newContent}`;
      const category = predictCategory(text);

      const resp = await fetch('/api/petitions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ title: newTitle, content: newContent, category })
      });
      if (resp.ok) {
        setNewTitle('');
        setNewContent('');
        await fetchPetitions();
      } else {
        setCreateError('Ошибка при создании петиции. Попробуйте ещё раз.');
      }
    } catch {
      setCreateError('Ошибка при работе ИИ. Попробуйте ещё раз.');
    } finally {
      setCreating(false);
    }
  }

  return (
    <div className="page">
      <h2>Профиль пользователя</h2>
      <p>Вы вошли как: <strong>{username}</strong></p>

      <section className="card">
        <h3>Создать новую петицию</h3>
        <div className="form-vertical">
          <input
            placeholder="Заголовок"
            value={newTitle}
            onChange={e => setNewTitle(e.target.value)}
          />
          <textarea
            placeholder="Текст петиции"
            value={newContent}
            onChange={e => setNewContent(e.target.value)}
          />
          {createError && <p className="error-text">{createError}</p>}
          <button onClick={createPetition} disabled={creating || !newTitle || !newContent}>
            {creating ? 'Создание и обработка...' : 'Создать петицию'}
          </button>
        </div>
      </section>

      <section>
        <h3>Мои петиции</h3>
        {loading ? (
          <p>Загрузка...</p>
        ) : myPetitions.length === 0 ? (
          <p>Вы ещё не создали ни одной петиции.</p>
        ) : (
          <ul className="petitions-list">
            {myPetitions.map(p => (
              <li key={p.id}>
                <h4>{p.title}</h4>
                <p>{p.content}</p>
                <small>
                  Статус: {(p as any).status ?? 'Новая'} · Подписей: {p.signatures}
                </small>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
