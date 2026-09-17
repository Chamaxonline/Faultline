const COOKIE_NAME = "faultline_session";

export type SessionUser = {
  sub: string;
  email: string;
  name: string;
  role: "Admin" | "Member";
  exp: number;
};

/** Decodes the JWT payload without verifying the signature — fine for UI display,
 *  the Api always re-validates the signature server-side for anything that matters. */
export function decodeToken(token: string): SessionUser | null {
  try {
    const payload = token.split(".")[1];
    const json = atob(payload.replace(/-/g, "+").replace(/_/g, "/"));
    const data = JSON.parse(json);
    return {
      sub: data.sub,
      email: data.email,
      name: data.name,
      role: data["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ?? data.role,
      exp: data.exp,
    };
  } catch {
    return null;
  }
}

export function isExpired(user: SessionUser): boolean {
  return user.exp * 1000 < Date.now();
}

// --- Client-side only (reads/writes the browser's document.cookie) ---

export function getClientToken(): string | undefined {
  if (typeof document === "undefined") return undefined;
  const match = document.cookie.match(new RegExp(`(?:^|; )${COOKIE_NAME}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : undefined;
}

export function setClientToken(token: string) {
  const secure = typeof window !== "undefined" && window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${COOKIE_NAME}=${encodeURIComponent(token)}; path=/; max-age=${60 * 60 * 8}; SameSite=Lax${secure}`;
}

export function clearClientToken() {
  document.cookie = `${COOKIE_NAME}=; path=/; max-age=0; SameSite=Lax`;
}

export const SESSION_COOKIE_NAME = COOKIE_NAME;
