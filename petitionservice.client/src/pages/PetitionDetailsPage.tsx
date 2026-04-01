import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

interface Petition {
  id: number;
  title: string;
  content: string;
  category?: string;
  createdAt: string;
  author: string;
  signatures: number;
}

export function PetitionDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [petition, setPetition] = useState<Petition | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [signed, setSigned] = useState(false);

  useEffect(() => {
    if (!id) return;
    fetchPetition(id);
  }, [id]);

  async function fetchPetition(petitionId: string) {
    setLoading(true);
    setError(null);
    try {
      const resp = await fetch(`/api/petitions/${petitionId}`);
      if (!resp.ok) {
        setError('Петиция не найдена');
        return;
      }
      const data: Petition = await resp.json();
      setPetition(data);
    } catch {
      setError('Ошибка загрузки петиции');
    } finally {
      setLoading(false);
    }
  }

  async function sign() {
    if (!petition) return;
    try {
      const resp = await fetch(`/api/petitions/${petition.id}/sign`, {
        method: 'POST'
      });
      if (resp.ok) {
        const data: Petition = await resp.json();
        setPetition(data);
        setSigned(true);
      } else if (resp.status === 401) {
        setError('Чтобы подписать петицию, необходимо войти в систему.');
      }
    } catch {
      // игнорируем для простоты
    }
  }

  if (loading) {
    return <div className="page"><p>Загрузка...</p></div>;
  }

  if (error || !petition) {
    return (
      <div className="page">
        <p className="error-text">{error ?? 'Петиция не найдена'}</p>
        <button onClick={() => navigate(-1)}>Назад</button>
      </div>
    );
  }

  return (
    <div className="page">
      <button onClick={() => navigate(-1)} style={{ marginBottom: '1rem' }}>
        Назад
      </button>
      <h2>{petition.title}</h2>
      <p style={{ color: '#4b5563', marginBottom: '0.5rem' }}>
        Автор: <strong>{petition.author}</strong>
      </p>
      {petition.category && (
        <p style={{ color: '#6b7280', marginBottom: '1rem' }}>
          Категория: {petition.category}
        </p>
      )}
      <div className="card" style={{ marginBottom: '1rem' }}>
        <p style={{ whiteSpace: 'pre-wrap' }}>{petition.content}</p>
      </div>
      <p style={{ marginBottom: '1rem' }}>Подписей: {petition.signatures}</p>
      <button onClick={sign} disabled={signed}>
        {signed ? 'Вы уже подписали эту петицию' : 'Подписать петицию'}
      </button>
    </div>
  );
}
