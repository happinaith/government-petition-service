import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';

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

export function PetitionsPage() {
  const [petitions, setPetitions] = useState<Petition[]>([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState<string>('all');

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

  return (
    <div className="page">
      <h2>Текущие петиции</h2>
      <div style={{ marginBottom: '1rem' }}>
        <label>
          Фильтр по статусу:&nbsp;
          <select
            value={statusFilter}
            onChange={e => setStatusFilter(e.target.value)}
          >
            <option value="all">Все</option>
            <option value="Новая">Новая</option>
            <option value="На рассмотрении">На рассмотрении</option>
            <option value="Принята">Принята</option>
            <option value="Отклонена">Отклонена</option>
            <option value="Закрыта">Закрыта</option>
          </select>
        </label>
      </div>
      {loading ? (
        <p>Загрузка...</p>
      ) : petitions.length === 0 ? (
        <p>Петиций пока нет.</p>
      ) : (
        <table className="petitions-table">
          <thead>
            <tr>
              <th>Заголовок</th>
              <th>Автор</th>
              <th>Категория</th>
              <th>Статус</th>
              <th>Подписей</th>
            </tr>
          </thead>
          <tbody>
            {petitions
              .filter(p =>
                statusFilter === 'all'
                  ? true
                  : (p.status ?? 'Новая') === statusFilter
              )
              .map(p => (
              <tr key={p.id}>
                <td>
                  <Link to={`/petitions/${p.id}`}>{p.title}</Link>
                </td>
                <td>{p.author}</td>
                <td>{p.category || '-'}</td>
                <td>{p.status ?? 'Новая'}</td>
                <td>{p.signatures}</td>
              </tr>
              ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
