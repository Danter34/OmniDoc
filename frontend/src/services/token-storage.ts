const TOKEN_KEY = "omnidoc.auth.token";

export const tokenStorage = {
  get() {
    if (typeof window === "undefined") {
      return null;
    }

    return window.localStorage.getItem(TOKEN_KEY);
  },

  set(token: string) {
    window.localStorage.setItem(TOKEN_KEY, token);
  },

  clear() {
    if (typeof window !== "undefined") {
      window.localStorage.removeItem(TOKEN_KEY);
    }
  },
};
