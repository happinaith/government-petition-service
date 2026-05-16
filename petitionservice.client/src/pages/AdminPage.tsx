import { useEffect, useState } from 'react';

interface Petition {
  id: number;
  title: string;
  content: string;
  category?: string;
  createdAt: string;
  author: string;
  signatures: number;
  status?: string;
}

export function AdminPage() {
  const [petitions, setPetitions] = useState<Petition[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchPetitions();
  }, []);

  async function fetchPetitions() {
    setLoading(true);
    setError(null);
    try {
      const resp = await fetch('/api/petitions');
      if (!resp.ok) {
        setError('Не удалось загрузить петиции');
        return;
      }
      const data = await resp.json();
      const list = Array.isArray(data) ? data : data.items;
      setPetitions(list ?? []);
    } catch {
      setError('Ошибка сети при загрузке петиций');
    } finally {
      setLoading(false);
    }
  }

  async function updateStatus(id: number, status: string) {
    try {
      const petition = petitions.find(p => p.id === id);
      if (!petition) return;
      const resp = await fetch(`/api/petitions/${id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ ...petition, status })
      });
      if (resp.ok) {
        await fetchPetitions();
      } else if (resp.status === 403) {
        setError('Недостаточно прав для изменения статуса');
      } else {
        setError('Ошибка при изменении статуса');
      }
    } catch {
      setError('Ошибка сети при изменении статуса');
    }
  }

  async function deletePetition(id: number) {
    if (!confirm('Вы уверены, что хотите удалить эту петицию?')) return;
    try {
      const resp = await fetch(`/api/petitions/${id}`, {
        method: 'DELETE'
      });
      if (resp.ok) {
        setPetitions(prev => prev.filter(p => p.id !== id));
      } else if (resp.status === 403) {
        setError('Недостаточно прав для удаления петиции');
      } else {
        setError('Ошибка при удалении петиции');
      }
    } catch {
      setError('Ошибка сети при удалении петиции');
    }
  }

  return (
    <div className="page">
      <h2>Админ-панель</h2>
      {error && <p className="error-text">{error}</p>}
      {loading ? (
        <p>Загрузка...</p>
      ) : petitions.length === 0 ? (
        <p>Петиций пока нет.</p>
      ) : (
        <table className="petitions-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Заголовок</th>
              <th>Автор</th>
              <th>Категория</th>
              <th>Статус</th>
              <th>Подписей</th>
              <th>Действия</th>
            </tr>
          </thead>
          <tbody>
            {petitions.map(p => (
              <tr key={p.id}>
                <td>{p.id}</td>
                <td>{p.title}</td>
                <td>{p.author}</td>
                <td>{p.category || '-'}</td>
                <td>{p.status ?? 'Новая'}</td>
                <td>{p.signatures}</td>
                <td>
                  <select
                    value={p.status ?? 'Новая'}
                    onChange={e => updateStatus(p.id, e.target.value)}
                  >
                    <option value="Новая">Новая</option>
                    <option value="На рассмотрении">На рассмотрении</option>
                    <option value="Принята">Принята</option>
                    <option value="Отклонена">Отклонена</option>
                    <option value="Закрыта">Закрыта</option>
                  </select>
                  <button onClick={() => deletePetition(p.id)} style={{ marginLeft: '0.5rem' }}>
                    Удалить
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
