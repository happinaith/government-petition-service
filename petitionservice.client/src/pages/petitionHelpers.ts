export interface PetitionPayload {
  title: string;
  content: string;
  category?: string;
}

export function toRoleLabel(role: string): string {
  if (role === "Admin") {
    return "Администратор";
  }

  if (role === "User") {
    return "Пользователь";
  }

  return role;
}

export function validatePetitionPayload(payload: PetitionPayload): string | null {
  if (payload.title.trim().length < 3 || payload.title.trim().length > 200) {
    return "Заголовок должен содержать от 3 до 200 символов.";
  }

  if (payload.content.trim().length < 10 || payload.content.trim().length > 5000) {
    return "Текст должен содержать от 10 до 5000 символов.";
  }

  if (payload.category && payload.category.trim().length > 100) {
    return "Категория должна содержать не более 100 символов.";
  }

  return null;
}

export async function readApiError(resp: Response): Promise<string> {
  try {
    const body = (await resp.json()) as { title?: string; detail?: string; errors?: Record<string, string[]> };
    const modelErrors = body.errors ? Object.values(body.errors).flat() : [];
    if (modelErrors.length > 0) {
      return modelErrors.join(" ");
    }

    if (body.detail) {
      return body.detail;
    }

    if (body.title) {
      return body.title;
    }
  } catch {
    // no-op
  }

  return `Запрос завершился ошибкой со статусом ${resp.status}.`;
}