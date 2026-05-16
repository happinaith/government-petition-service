import { describe, expect, it } from 'vitest';
import { readApiError, toRoleLabel, validatePetitionPayload } from './petitionHelpers';

function responseFrom(body: unknown, status = 400): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('petition helpers', () => {
  it('maps roles to labels', () => {
    expect(toRoleLabel('Admin')).toBe('Администратор');
    expect(toRoleLabel('User')).toBe('Пользователь');
    expect(toRoleLabel('Support')).toBe('Support');
  });

  it('validates petition payloads', () => {
    expect(validatePetitionPayload({ title: 'Hi', content: 'Short text' })).toBe(
      'Заголовок должен содержать от 3 до 200 символов.',
    );
    expect(
      validatePetitionPayload({
        title: 'Valid title',
        content: 'short',
      }),
    ).toBe('Текст должен содержать от 10 до 5000 символов.');
    expect(
      validatePetitionPayload({
        title: 'Valid title',
        content: 'This content is long enough for validation.',
        category: 'x'.repeat(101),
      }),
    ).toBe('Категория должна содержать не более 100 символов.');
    expect(
      validatePetitionPayload({
        title: 'Valid title',
        content: 'This content is long enough for validation.',
      }),
    ).toBeNull();
  });

  it('extracts the most useful server error text', async () => {
    await expect(
      readApiError(
        responseFrom(
          {
            title: 'Validation failed',
            detail: 'Ignored detail',
            errors: { title: ['Title is required'], content: ['Content is required'] },
          },
          400,
        ),
      ),
    ).resolves.toBe('Title is required Content is required');
  });

  it('falls back to status text when the body is not useful', async () => {
    const resp = new Response('plain text', { status: 503 });

    await expect(readApiError(resp)).resolves.toBe('Запрос завершился ошибкой со статусом 503.');
  });
});