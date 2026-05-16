import { describe, expect, it } from 'vitest';
import { getJwtExpirationMs, isJwtExpired } from './jwt';

function makeJwt(payload: Record<string, unknown>): string {
  const encoded = Buffer.from(JSON.stringify(payload)).toString('base64url');
  return `header.${encoded}.signature`;
}

describe('jwt helpers', () => {
  it('reads expiration from a token payload', () => {
    const token = makeJwt({ exp: 1_700_000_000 });

    expect(getJwtExpirationMs(token)).toBe(1_700_000_000_000);
  });

  it('treats malformed tokens as expired', () => {
    expect(isJwtExpired('invalid-token')).toBe(true);
  });

  it('honors leeway when checking expiration', () => {
    const token = makeJwt({ exp: Math.floor((Date.now() + 10_000) / 1000) });

    expect(isJwtExpired(token, 30_000)).toBe(true);
    expect(isJwtExpired(token, 0)).toBe(false);
  });
});