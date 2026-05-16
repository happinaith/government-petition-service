import { useCallback, useEffect, useRef, useState } from "react";
import type { ReactElement } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import { ROUTES } from "../routing/routes";
import { SeoHead } from "../seo/SeoHead";
import { readApiError, toRoleLabel, validatePetitionPayload, type PetitionPayload } from "./petitionHelpers";
import type {
  Petition,
  PetitionListItem,
  PetitionAttachment,
  PetitionAiAssistResponse,
  PetitionListResponse,
  PreSignedDownloadResponse,
} from "../auth/types";

const MAX_ATTACHMENT_BYTES = 5 * 1024 * 1024;
const ALLOWED_ATTACHMENT_TYPES = ["application/pdf", "image/png", "image/jpeg", "text/plain"];
type UploadStatus = "idle" | "uploading" | "success" | "error";
type AiAssistState = "idle" | "loading" | "success" | "empty" | "error" | "unavailable";

export function PetitionsPage(): ReactElement {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { session, hasRole, logout, authFetch } = useAuth();
  const [petitions, setPetitions] = useState<PetitionListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [currentPageSize, setCurrentPageSize] = useState(10);
  const [loading, setLoading] = useState(true);
  const [newTitle, setNewTitle] = useState("");
  const [newContent, setNewContent] = useState("");
  const [newCategory, setNewCategory] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editTitle, setEditTitle] = useState("");
  const [editContent, setEditContent] = useState("");
  const [editCategory, setEditCategory] = useState("");
  const [selectedPetition, setSelectedPetition] = useState<Petition | null>(null);
  const [selectedAttachments, setSelectedAttachments] = useState<PetitionAttachment[]>([]);
  const [attachmentFile, setAttachmentFile] = useState<File | null>(null);
  const [attachmentsLoading, setAttachmentsLoading] = useState(false);
  const [attachmentUploadStatus, setAttachmentUploadStatus] = useState<UploadStatus>("idle");
  const [attachmentStatusMessage, setAttachmentStatusMessage] = useState<string | null>(null);
  const [attachmentErrorMessage, setAttachmentErrorMessage] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [grantAdminUsername, setGrantAdminUsername] = useState("");
  const [grantStatus, setGrantStatus] = useState<string | null>(null);
  const [aiDraftBusy, setAiDraftBusy] = useState(false);
  const [aiAssistState, setAiAssistState] = useState<AiAssistState>("idle");
  const [aiAssistError, setAiAssistError] = useState<string | null>(null);
  const [aiAssistUnavailableReason, setAiAssistUnavailableReason] = useState<string | null>(null);
  const [aiAssistResult, setAiAssistResult] = useState<PetitionAiAssistResponse | null>(null);
  const [aiDraftSummary, setAiDraftSummary] = useState<string | null>(null);
  const petitionsCacheRef = useRef<Map<string, PetitionListResponse>>(new Map());

  const canCreate = hasRole("User") || hasRole("Admin");
  const isAdmin = hasRole("Admin");

  const query = searchParams.get("q") ?? "";
  const category = searchParams.get("category") ?? "";
  const author = searchParams.get("author") ?? "";
  const minSignatures = searchParams.get("minSignatures") ?? "";
  const maxSignatures = searchParams.get("maxSignatures") ?? "";
  const sortBy = searchParams.get("sortBy") ?? "createdAt";
  const sortDir = searchParams.get("sortDir") ?? "desc";
  const page = Math.max(1, Number.parseInt(searchParams.get("page") ?? "1", 10) || 1);
  const pageSize = Math.max(1, Number.parseInt(searchParams.get("pageSize") ?? "10", 10) || 10);
  const searchQueryString = searchParams.toString();

  const [queryDraft, setQueryDraft] = useState(query);
  const [categoryDraft, setCategoryDraft] = useState(category);
  const [authorDraft, setAuthorDraft] = useState(author);
  const [minSignaturesDraft, setMinSignaturesDraft] = useState(minSignatures);
  const [maxSignaturesDraft, setMaxSignaturesDraft] = useState(maxSignatures);

  useEffect(() => {
    setQueryDraft(query);
    setCategoryDraft(category);
    setAuthorDraft(author);
    setMinSignaturesDraft(minSignatures);
    setMaxSignaturesDraft(maxSignatures);
  }, [author, category, maxSignatures, minSignatures, query]);

  const updateParams = (updates: Record<string, string>, resetPage = false): void => {
    const next = new URLSearchParams(searchParams);

    Object.entries(updates).forEach(([key, value]) => {
      if (value.trim()) {
        next.set(key, value.trim());
      } else {
        next.delete(key);
      }
    });

    if (resetPage) {
      next.set("page", "1");
    }

    setSearchParams(next);
  };

  const fetchPetitions = useCallback(async (forceRefresh = false): Promise<void> => {
    const cacheKey = searchQueryString || "__default__";
    if (!forceRefresh) {
      const cached = petitionsCacheRef.current.get(cacheKey);
      if (cached) {
        setPetitions(cached.items);
        setTotalCount(cached.totalCount);
        setCurrentPage(cached.page);
        setCurrentPageSize(cached.pageSize);
        setLoading(false);
        return;
      }
    }

    setLoading(true);
    try {
      const resp = await authFetch(`/api/petitions?${searchQueryString}`);
      if (resp.ok) {
        const data = (await resp.json()) as PetitionListResponse;
        setPetitions(data.items);
        setTotalCount(data.totalCount);
        setCurrentPage(data.page);
        setCurrentPageSize(data.pageSize);
        petitionsCacheRef.current.set(cacheKey, data);
      } else {
        setPetitions([]);
        setTotalCount(0);
      }
    } finally {
      setLoading(false);
    }
  }, [authFetch, searchQueryString]);

  useEffect(() => {
    void fetchPetitions();
  }, [fetchPetitions]);

  const applyDraftFilters = (): void => {
    updateParams(
      {
        q: queryDraft,
        category: categoryDraft,
        author: authorDraft,
        minSignatures: minSignaturesDraft,
        maxSignatures: maxSignaturesDraft,
      },
      true,
    );
  };

  const clearListCache = (): void => {
    petitionsCacheRef.current.clear();
  };

  const loadPetitionDetails = async (petitionId: number): Promise<Petition | null> => {
    const resp = await authFetch(`/api/petitions/${petitionId}`);
    if (!resp.ok) {
      if (resp.status === 404) {
        setErrorMessage("Петиция не найдена.");
      } else {
        setErrorMessage(await readApiError(resp));
      }

      return null;
    }

    const detail = (await resp.json()) as Petition;
    return detail;
  };

  const onLogout = async (): Promise<void> => {
    await logout();
    navigate(ROUTES.AUTH_LOGIN, { replace: true });
  };

  const sign = async (id: number): Promise<void> => {
    const resp = await authFetch(`/api/petitions/${id}/sign`, { method: "POST" });
    if (resp.ok) {
      clearListCache();
      await fetchPetitions(true);
    }
  };

  const grantAdmin = async (): Promise<void> => {
    setGrantStatus(null);
    const resp = await authFetch("/api/auth/grant-admin", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username: grantAdminUsername }),
    });

    if (resp.ok) {
      setGrantStatus("Роль администратора выдана.");
      setGrantAdminUsername("");
      return;
    }

    setGrantStatus("Не удалось выдать роль администратора.");
  };

  const generateAiDraft = async (): Promise<void> => {
    setErrorMessage(null);
    setFeedback(null);
    setAiDraftSummary(null);
    setAiAssistError(null);
    setAiAssistUnavailableReason(null);
    setAiAssistResult(null);

    if (newContent.trim().length < 10) {
      setAiAssistState("error");
      setAiAssistError("Введите минимум 10 символов текста, чтобы AI подготовил черновик.");
      return;
    }

    setAiDraftBusy(true);
    setAiAssistState("loading");
    try {
      const resp = await authFetch("/api/petitions/ai-draft", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          content: newContent,
          titleHint: newTitle || undefined,
          categoryHint: newCategory || undefined,
        }),
      });

      if (!resp.ok) {
        const apiError = await readApiError(resp);
        if (resp.status === 502 || resp.status === 503 || resp.status === 504 || resp.status === 429) {
          setAiAssistState("unavailable");
          setAiAssistUnavailableReason(apiError);
          setFeedback("AI-сервис временно недоступен. Можно продолжить создание петиции вручную.");
        } else {
          setAiAssistState("error");
          setAiAssistError(apiError);
        }
        return;
      }

      const suggestion = (await resp.json()) as PetitionAiAssistResponse;
      if (!suggestion.title?.trim() || !suggestion.content?.trim()) {
        setAiAssistState("empty");
        return;
      }

      setNewTitle(suggestion.title);
      setNewContent(suggestion.content);
      setNewCategory(suggestion.category ?? "");
      setAiDraftSummary(suggestion.summary);
      setAiAssistResult(suggestion);
      setAiAssistState("success");
      setFeedback(`Черновик обновлен через ${suggestion.provider} (${suggestion.model}).`);
    } catch {
      setAiAssistState("unavailable");
      setAiAssistUnavailableReason("Сервис AI временно недоступен из-за сетевой ошибки.");
      setFeedback("AI-сервис временно недоступен. Можно продолжить создание петиции вручную.");
    } finally {
      setAiDraftBusy(false);
    }
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / currentPageSize));
  const hasActiveFilters = [query, category, author, minSignatures, maxSignatures].some((value) => value.trim().length > 0);
  const petitionsTitle = hasActiveFilters
    ? `Петиции: фильтры и поиск | Страница ${currentPage}`
    : `Список петиций | Страница ${currentPage}`;
  const petitionsDescription = hasActiveFilters
    ? `Отфильтрованный список петиций. Найдено: ${totalCount}. Страница ${currentPage} из ${totalPages}.`
    : `Рабочий список петиций с возможностью сортировки, редактирования и подписи. Найдено: ${totalCount}.`;
  const petitionsStructuredData: Record<string, unknown> = {
    "@context": "https://schema.org",
    "@type": "CollectionPage",
    name: "Петиции",
    description: petitionsDescription,
    url: new URL(ROUTES.PETITIONS, window.location.origin).toString(),
    inLanguage: "ru",
    mainEntity: {
      "@type": "ItemList",
      numberOfItems: totalCount,
    },
  };

  const canManagePetition = (petition: { author: string }): boolean => {
    return isAdmin || petition.author === session?.username;
  };

  const startEdit = (petition: Petition): void => {
    setEditingId(petition.id);
    setEditTitle(petition.title);
    setEditContent(petition.content);
    setEditCategory(petition.category ?? "");
    setFeedback(null);
    setErrorMessage(null);
  };

  const cancelEdit = (): void => {
    setEditingId(null);
    setEditTitle("");
    setEditContent("");
    setEditCategory("");
  };

  const loadAttachments = async (petitionId: number): Promise<void> => {
    setAttachmentsLoading(true);
    setAttachmentErrorMessage(null);
    try {
      const resp = await authFetch(`/api/petitions/${petitionId}/attachments`, {}, true);
      if (!resp.ok) {
        if (resp.status === 401 || resp.status === 403) {
          setAttachmentErrorMessage("У вас нет доступа к списку вложений.");
        } else {
          setAttachmentErrorMessage(await readApiError(resp));
        }
        setSelectedAttachments([]);
        return;
      }

      const data = (await resp.json()) as PetitionAttachment[];
      setSelectedAttachments(data);
    } finally {
      setAttachmentsLoading(false);
    }
  };

  const requestPreSignedUrl = async (
    petitionId: number,
    attachmentId: number,
    inline = false,
  ): Promise<string | null> => {
    const urlResp = await authFetch(
      `/api/petitions/${petitionId}/attachments/${attachmentId}/presigned-download?inline=${String(inline)}`,
      { method: "POST" },
      true,
    );

    if (!urlResp.ok) {
      if (urlResp.status === 401 || urlResp.status === 403) {
        setAttachmentErrorMessage("У вас нет доступа к этому файлу.");
      } else {
        setAttachmentErrorMessage(await readApiError(urlResp));
      }

      return null;
    }

    const payload = (await urlResp.json()) as PreSignedDownloadResponse;
    return payload.url;
  };

  return (
    <main>
      <SeoHead
        title={petitionsTitle}
        description={petitionsDescription}
        canonicalPath={ROUTES.PETITIONS}
        robots="noindex, nofollow"
        structuredData={petitionsStructuredData}
      />

      <header className="toolbar">
        <div>
          <img src="/vite.svg" alt="Логотип платформы петиций" width={40} height={40} loading="lazy" decoding="async" />
          <h1>Петиции</h1>
          <p>
            Вы вошли как <strong>{session?.username}</strong>
          </p>
          <p>Роли: {session?.roles.map(toRoleLabel).join(", ")}</p>
        </div>
        <button onClick={() => void onLogout()}>Выйти</button>
      </header>

      {canCreate ? (
        <section className="card">
          <h2>Создать петицию</h2>
          {errorMessage ? <p className="error">{errorMessage}</p> : null}
          {feedback ? <p className="success">{feedback}</p> : null}
          {aiDraftSummary ? <p>{aiDraftSummary}</p> : null}
          <input placeholder="Заголовок" value={newTitle} onChange={(e) => setNewTitle(e.target.value)} />
          <input placeholder="Категория" value={newCategory} onChange={(e) => setNewCategory(e.target.value)} />
          <textarea placeholder="Текст" value={newContent} onChange={(e) => setNewContent(e.target.value)} />
          <button
            type="button"
            onClick={() => void generateAiDraft()}
            disabled={aiDraftBusy || newContent.trim().length < 10}
          >
            {aiDraftBusy ? "AI готовит черновик..." : "Сгенерировать черновик через AI"}
          </button>
          <section className="ai-assist-panel" aria-live="polite">
            <h3>AI-помощник</h3>
            {aiAssistState === "idle" ? <p>AI-черновик еще не запрошен.</p> : null}
            {aiAssistState === "loading" ? <p className="loading-state">Запрос к внешнему API...</p> : null}
            {aiAssistState === "empty" ? <p>Внешний API вернул пустой результат. Попробуйте уточнить текст и повторить.</p> : null}
            {aiAssistState === "error" && aiAssistError ? <p className="error">{aiAssistError}</p> : null}
            {aiAssistState === "unavailable" ? (
              <div>
                <p className="error">Внешний AI API временно недоступен.</p>
                {aiAssistUnavailableReason ? <p>{aiAssistUnavailableReason}</p> : null}
                <p>Graceful degradation: продолжайте работу вручную, отправка петиции остается доступной.</p>
              </div>
            ) : null}
            {aiAssistState === "success" && aiAssistResult ? (
              <div className="ai-assist-result">
                <p>
                  Источник: <strong>{aiAssistResult.provider}</strong> ({aiAssistResult.model})
                </p>
                <p>
                  Предложенный заголовок: <strong>{aiAssistResult.title}</strong>
                </p>
                <p>Категория: {aiAssistResult.category || "Не указана"}</p>
                <p>{aiAssistResult.summary}</p>
              </div>
            ) : null}
          </section>
          <button
            onClick={() => void (async () => {
              setFeedback(null);
              setErrorMessage(null);

              const payload: PetitionPayload = {
                title: newTitle,
                content: newContent,
                category: newCategory || undefined,
              };

              const validationError = validatePetitionPayload(payload);
              if (validationError) {
                setErrorMessage(validationError);
                return;
              }

              const resp = await authFetch("/api/petitions", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload),
              });

              if (resp.ok) {
                clearListCache();
                setNewTitle("");
                setNewContent("");
                setNewCategory("");
                setFeedback("Петиция успешно создана.");
                await fetchPetitions(true);
                return;
              }

              if (resp.status === 403) {
                setErrorMessage("У вас нет прав на создание петиций.");
                return;
              }

              if (resp.status === 409) {
                setErrorMessage(await readApiError(resp));
                return;
              }

              setErrorMessage(await readApiError(resp));
            })()}
            disabled={!newTitle.trim() || !newContent.trim()}
          >
            Создать
          </button>
        </section>
      ) : null}

      {isAdmin ? (
        <section className="card">
          <h2>Инструменты администратора</h2>
          <input
            placeholder="Логин для выдачи роли администратора"
            value={grantAdminUsername}
            onChange={(e) => setGrantAdminUsername(e.target.value)}
          />
          <button onClick={() => void grantAdmin()} disabled={!grantAdminUsername.trim()}>
            Выдать роль администратора
          </button>
          {grantStatus ? <p>{grantStatus}</p> : null}
        </section>
      ) : null}

      <section className="card">
        <h2>Фильтры и поиск</h2>
        <form
          className="filters-grid"
          onSubmit={(event) => {
            event.preventDefault();
            applyDraftFilters();
          }}
        >
          <input
            placeholder="Поиск по заголовку, тексту, автору"
            value={queryDraft}
            onChange={(e) => setQueryDraft(e.target.value)}
          />
          <input
            placeholder="Категория"
            value={categoryDraft}
            onChange={(e) => setCategoryDraft(e.target.value)}
          />
          <input
            placeholder="Автор"
            value={authorDraft}
            onChange={(e) => setAuthorDraft(e.target.value)}
          />
          <input
            type="number"
            min={0}
            placeholder="Подписей от"
            value={minSignaturesDraft}
            onChange={(e) => setMinSignaturesDraft(e.target.value)}
          />
          <input
            type="number"
            min={0}
            placeholder="Подписей до"
            value={maxSignaturesDraft}
            onChange={(e) => setMaxSignaturesDraft(e.target.value)}
          />
          <select value={sortBy} onChange={(e) => updateParams({ sortBy: e.target.value }, true)}>
            <option value="createdAt">Сортировка по дате</option>
            <option value="title">Сортировка по заголовку</option>
            <option value="author">Сортировка по автору</option>
            <option value="signatures">Сортировка по подписям</option>
          </select>
          <select value={sortDir} onChange={(e) => updateParams({ sortDir: e.target.value }, true)}>
            <option value="desc">По убыванию</option>
            <option value="asc">По возрастанию</option>
          </select>
          <select value={String(pageSize)} onChange={(e) => updateParams({ pageSize: e.target.value }, true)}>
            <option value="5">5 на страницу</option>
            <option value="10">10 на страницу</option>
            <option value="20">20 на страницу</option>
            <option value="50">50 на страницу</option>
          </select>
          <button type="submit">Применить фильтры</button>
          <button
            type="button"
            onClick={() => {
              setSearchParams(new URLSearchParams());
            }}
          >
            Сбросить фильтры
          </button>
        </form>
      </section>

      <section className="card">
        <h2>Все петиции</h2>
        <p>
          Всего: <strong>{totalCount}</strong>
        </p>
        {errorMessage ? <p className="error">{errorMessage}</p> : null}
        {feedback ? <p className="success">{feedback}</p> : null}
        {loading ? <p className="loading-state" aria-live="polite">Загрузка...</p> : null}
        {!loading ? (
          <>
            <div className="table-shell">
            <table>
              <thead>
                <tr>
                  <th>Заголовок</th>
                  <th>Автор</th>
                  <th>Категория</th>
                  <th>Подписи</th>
                  <th>Действия</th>
                </tr>
              </thead>
              <tbody>
                {petitions.map((petition) => (
                  <tr key={petition.id}>
                    <td>{petition.title}</td>
                    <td>{petition.author}</td>
                    <td>{petition.category || "-"}</td>
                    <td>{petition.signatures}</td>
                    <td>
                      <button
                        onClick={() => void (async () => {
                          setFeedback(null);
                          setErrorMessage(null);
                          const detail = await loadPetitionDetails(petition.id);
                          if (!detail) {
                            return;
                          }

                          setSelectedPetition(detail);
                          await loadAttachments(petition.id);
                        })()}
                      >
                        Открыть
                      </button>
                      <button onClick={() => void sign(petition.id)}>Подписать</button>
                      {canManagePetition(petition) ? (
                        <button
                          onClick={() => void (async () => {
                            setFeedback(null);
                            setErrorMessage(null);
                            const detail = await loadPetitionDetails(petition.id);
                            if (!detail) {
                              return;
                            }

                            startEdit(detail);
                          })()}
                        >
                          Редактировать
                        </button>
                      ) : null}
                      {canManagePetition(petition) ? (
                        <button
                          onClick={() => void (async () => {
                            setFeedback(null);
                            setErrorMessage(null);
                            const resp = await authFetch(`/api/petitions/${petition.id}`, { method: "DELETE" });

                            if (resp.ok) {
                              clearListCache();
                              if (selectedPetition?.id === petition.id) {
                                setSelectedPetition(null);
                              }

                              setFeedback("Петиция удалена.");
                              await fetchPetitions(true);
                              return;
                            }

                            if (resp.status === 403) {
                              setErrorMessage("У вас нет прав на удаление этой петиции.");
                              return;
                            }

                            if (resp.status === 404) {
                              setErrorMessage("Петиция не найдена.");
                              clearListCache();
                              await fetchPetitions(true);
                              return;
                            }

                            setErrorMessage(await readApiError(resp));
                          })()}
                        >
                          Удалить
                        </button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>

            {selectedPetition ? (
              <article className="card details-card" aria-labelledby="petition-details-title">
                <h3 id="petition-details-title">Детали петиции</h3>
                <p>
                  <strong>Заголовок:</strong> {selectedPetition.title}
                </p>
                <p>
                  <strong>Автор:</strong> {selectedPetition.author}
                </p>
                <p>
                  <strong>Категория:</strong> {selectedPetition.category || "-"}
                </p>
                <p>
                  <strong>Создана:</strong> {new Date(selectedPetition.createdAt).toLocaleString()}
                </p>
                <p>
                  <strong>Подписей:</strong> {selectedPetition.signatures}
                </p>
                <p>
                  <strong>Текст:</strong>
                </p>
                <p>{selectedPetition.content}</p>

                <h4>Вложения</h4>
                {attachmentErrorMessage ? <p className="error">{attachmentErrorMessage}</p> : null}
                {attachmentsLoading ? <p>Загрузка вложений...</p> : null}
                {!attachmentsLoading && selectedAttachments.length === 0 ? <p>Вложений нет.</p> : null}
                {!attachmentsLoading && selectedAttachments.length > 0 ? (
                  <table>
                    <thead>
                      <tr>
                        <th>Имя</th>
                        <th>Тип</th>
                        <th>Размер</th>
                        <th>Действия</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedAttachments.map((attachment) => (
                        <tr key={attachment.id}>
                          <td>{attachment.fileName}</td>
                          <td>{attachment.contentType}</td>
                          <td>{Math.ceil(attachment.sizeBytes / 1024)} КБ</td>
                          <td>
                            <button
                              onClick={() => void (async () => {
                                setAttachmentErrorMessage(null);
                                const url = await requestPreSignedUrl(selectedPetition.id, attachment.id, false);
                                if (!url) {
                                  return;
                                }

                                const resolvedUrl = new URL(url, window.location.origin).toString();
                                window.open(resolvedUrl, "_blank", "noopener,noreferrer");
                              })()}
                            >
                              Скачать
                            </button>
                            <button
                              onClick={() => void (async () => {
                                setAttachmentErrorMessage(null);
                                const url = await requestPreSignedUrl(selectedPetition.id, attachment.id, true);
                                if (!url) {
                                  return;
                                }

                                const resolvedUrl = new URL(url, window.location.origin).toString();
                                const previewWindow = window.open(resolvedUrl, "_blank", "noopener,noreferrer");
                                if (!previewWindow) {
                                  setAttachmentErrorMessage("Не удалось открыть окно предпросмотра.");
                                  return;
                                }
                              })()}
                            >
                              Просмотр
                            </button>
                            {canManagePetition(selectedPetition) ? (
                              <button
                                onClick={() => void (async () => {
                                  setAttachmentErrorMessage(null);

                                  const resp = await authFetch(
                                    `/api/petitions/${selectedPetition.id}/attachments/${attachment.id}`,
                                    { method: "DELETE" },
                                    true,
                                  );

                                  if (resp.ok) {
                                    setFeedback("Вложение удалено.");
                                    await loadAttachments(selectedPetition.id);
                                    return;
                                  }

                                  if (resp.status === 401 || resp.status === 403) {
                                    setAttachmentErrorMessage("У вас нет прав на удаление этого файла.");
                                    return;
                                  }

                                  setAttachmentErrorMessage(await readApiError(resp));
                                })()}
                              >
                                Удалить
                              </button>
                            ) : null}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                ) : null}

                {canManagePetition(selectedPetition) ? (
                  <div className="attachment-upload">
                    <input
                      type="file"
                      accept=".pdf,.png,.jpg,.jpeg,.txt"
                      onChange={(e) => setAttachmentFile(e.target.files?.[0] ?? null)}
                    />
                    <button
                      disabled={!attachmentFile || attachmentUploadStatus === "uploading"}
                      onClick={() => void (async () => {
                        setAttachmentErrorMessage(null);
                        setAttachmentUploadStatus("idle");
                        setAttachmentStatusMessage(null);

                        if (!attachmentFile) {
                          setAttachmentErrorMessage("Сначала выберите файл.");
                          return;
                        }

                        if (attachmentFile.size > MAX_ATTACHMENT_BYTES) {
                          setAttachmentErrorMessage("Файл слишком большой. Максимум 5 МБ.");
                          return;
                        }

                        if (!ALLOWED_ATTACHMENT_TYPES.includes(attachmentFile.type)) {
                          setAttachmentErrorMessage("Неподдерживаемый тип файла. Разрешены: PDF, PNG, JPG, TXT.");
                          return;
                        }

                        setAttachmentUploadStatus("uploading");
                        setAttachmentStatusMessage(`Загрузка ${attachmentFile.name}...`);

                        const formData = new FormData();
                        formData.append("file", attachmentFile);

                        const resp = await authFetch(
                          `/api/petitions/${selectedPetition.id}/attachments`,
                          {
                            method: "POST",
                            body: formData,
                          },
                          true,
                        );

                        if (resp.ok) {
                          setAttachmentFile(null);
                          setAttachmentUploadStatus("success");
                          setAttachmentStatusMessage("Файл успешно загружен.");
                          await loadAttachments(selectedPetition.id);
                          return;
                        }

                        setAttachmentUploadStatus("error");

                        if (resp.status === 401 || resp.status === 403) {
                          setAttachmentErrorMessage("У вас нет прав на загрузку файлов для этой петиции.");
                          return;
                        }

                        if (resp.status === 409) {
                          setAttachmentErrorMessage(await readApiError(resp));
                          return;
                        }

                        setAttachmentErrorMessage(await readApiError(resp));
                      })()}
                    >
                      Загрузить файл
                    </button>
                    {attachmentUploadStatus === "uploading" ? <p className="upload-status">Загрузка...</p> : null}
                    {attachmentStatusMessage ? <p className="success">{attachmentStatusMessage}</p> : null}
                    <p>Разрешено: PDF, PNG, JPG, TXT. Максимальный размер: 5 МБ.</p>
                  </div>
                ) : null}
              </article>
            ) : null}

            {editingId !== null ? (
              <section className="card details-card">
                <h3>Редактирование петиции</h3>
                <input value={editTitle} onChange={(e) => setEditTitle(e.target.value)} placeholder="Заголовок" />
                <input value={editCategory} onChange={(e) => setEditCategory(e.target.value)} placeholder="Категория" />
                <textarea value={editContent} onChange={(e) => setEditContent(e.target.value)} placeholder="Текст" />
                <div className="actions">
                  <button
                    onClick={() => void (async () => {
                      setFeedback(null);
                      setErrorMessage(null);

                      const payload: PetitionPayload = {
                        title: editTitle,
                        content: editContent,
                        category: editCategory || undefined,
                      };

                      const validationError = validatePetitionPayload(payload);
                      if (validationError) {
                        setErrorMessage(validationError);
                        return;
                      }

                      const resp = await authFetch(`/api/petitions/${editingId}`, {
                        method: "PUT",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify(payload),
                      });

                      if (resp.ok) {
                        clearListCache();
                        setFeedback("Петиция обновлена.");
                        cancelEdit();
                        await fetchPetitions(true);
                        return;
                      }

                      if (resp.status === 403) {
                        setErrorMessage("У вас нет прав на редактирование этой петиции.");
                        return;
                      }

                      if (resp.status === 404) {
                        setErrorMessage("Петиция не найдена.");
                        cancelEdit();
                        clearListCache();
                        await fetchPetitions(true);
                        return;
                      }

                      if (resp.status === 409) {
                        setErrorMessage(await readApiError(resp));
                        return;
                      }

                      setErrorMessage(await readApiError(resp));
                    })()}
                  >
                    Сохранить
                  </button>
                  <button type="button" onClick={cancelEdit}>
                    Отмена
                  </button>
                </div>
              </section>
            ) : null}

            <div className="pagination-row">
              <button
                onClick={() => updateParams({ page: String(Math.max(1, page - 1)) })}
                disabled={page <= 1}
              >
                Назад
              </button>
              <span>
                Страница <strong>{currentPage}</strong> из <strong>{totalPages}</strong>
              </span>
              <button
                onClick={() => updateParams({ page: String(Math.min(totalPages, page + 1)) })}
                disabled={page >= totalPages}
              >
                Вперёд
              </button>
            </div>
          </>
        ) : null}
      </section>
    </main>
  );
}
