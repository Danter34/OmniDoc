import { SESSION_COOKIE, SESSION_COOKIE_MAX_AGE, TOKEN_STORAGE_KEY } from "@/lib/session";

function syncSessionCookie(token: string) {
  const secure = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie = `${SESSION_COOKIE}=${encodeURIComponent(token)}; Path=/; Max-Age=${SESSION_COOKIE_MAX_AGE}; SameSite=Lax${secure}`;
}

export const tokenStorage = {
  get() {
    if (typeof window === "undefined") {
      return null;
    }

    const token = window.localStorage.getItem(TOKEN_STORAGE_KEY);
    if (token) syncSessionCookie(token);
    return token;
  },

  set(token: string) {
    window.localStorage.setItem(TOKEN_STORAGE_KEY, token);
    syncSessionCookie(token);
  },

  clear() {
    if (typeof window !== "undefined") {
      window.localStorage.removeItem(TOKEN_STORAGE_KEY);
      document.cookie = `${SESSION_COOKIE}=; Path=/; Max-Age=0; SameSite=Lax`;
    }
  },
};
