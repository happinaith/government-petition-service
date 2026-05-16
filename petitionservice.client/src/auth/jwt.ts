function decodeBase64Url(value: string): string {
  const normalized = value.replace(/-/g, "+").replace(/_/g, "/");
  const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "=");
  return atob(padded);
}

export function getJwtExpirationMs(token: string): number | null {
  try {
    const parts = token.split(".");
    if (parts.length < 2) {
      return null;
    }

    const payloadJson = decodeBase64Url(parts[1]);
    const payload = JSON.parse(payloadJson) as { exp?: number };
    if (!payload.exp) {
      return null;
    }

    return payload.exp * 1000;
  } catch {
    return null;
  }
}

export function isJwtExpired(token: string, leewayMs = 30_000): boolean {
  const exp = getJwtExpirationMs(token);
  if (!exp) {
    return true;
  }

  return Date.now() + leewayMs >= exp;
}
