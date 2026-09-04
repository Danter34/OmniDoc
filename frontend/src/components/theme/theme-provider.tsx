"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";

export type ThemePreference = "system" | "light" | "dark";
export type ResolvedTheme = Exclude<ThemePreference, "system">;

interface ThemeContextValue {
  theme: ThemePreference;
  resolvedTheme: ResolvedTheme;
  setTheme: (theme: ThemePreference) => void;
}

const STORAGE_KEY = "omnidoc.theme";
const COOKIE_KEY = "omnidoc-theme";
const COOKIE_MAX_AGE = 60 * 60 * 24 * 365;
const SYSTEM_THEME_QUERY = "(prefers-color-scheme: dark)";

const ThemeContext = createContext<ThemeContextValue | null>(null);

function isThemePreference(value: string | null): value is ThemePreference {
  return value === "system" || value === "light" || value === "dark";
}

function readCookiePreference(): ThemePreference | null {
  if (typeof document === "undefined") {
    return null;
  }

  const cookie = document.cookie
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(`${COOKIE_KEY}=`));
  const value = cookie?.slice(COOKIE_KEY.length + 1) ?? null;

  return isThemePreference(value) ? value : null;
}

function readStoredPreference(): ThemePreference {
  if (typeof window === "undefined") {
    return "system";
  }

  try {
    const storedTheme = window.localStorage.getItem(STORAGE_KEY);
    if (isThemePreference(storedTheme)) {
      return storedTheme;
    }
  } catch {
    // Storage may be unavailable in privacy-restricted browsing contexts.
  }

  return readCookiePreference() ?? "system";
}

function resolveTheme(preference: ThemePreference): ResolvedTheme {
  if (preference !== "system") {
    return preference;
  }

  return window.matchMedia(SYSTEM_THEME_QUERY).matches ? "dark" : "light";
}

function readInitialResolvedTheme(): ResolvedTheme {
  if (typeof window === "undefined") {
    return "light";
  }

  const bootstrappedTheme = document.documentElement.dataset.theme;
  if (bootstrappedTheme === "light" || bootstrappedTheme === "dark") {
    return bootstrappedTheme;
  }

  return resolveTheme(readStoredPreference());
}

function applyResolvedTheme(theme: ResolvedTheme) {
  const root = document.documentElement;
  root.dataset.theme = theme;
  root.style.colorScheme = theme;
}

function persistPreference(theme: ThemePreference) {
  try {
    window.localStorage.setItem(STORAGE_KEY, theme);
  } catch {
    // The cookie remains the persistence fallback when localStorage is blocked.
  }

  document.cookie = `${COOKIE_KEY}=${theme}; Path=/; Max-Age=${COOKIE_MAX_AGE}; SameSite=Lax; Secure`;
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setThemeState] = useState<ThemePreference>(readStoredPreference);
  const [resolvedTheme, setResolvedTheme] =
    useState<ResolvedTheme>(readInitialResolvedTheme);

  const setTheme = useCallback((nextTheme: ThemePreference) => {
    const nextResolvedTheme = resolveTheme(nextTheme);

    persistPreference(nextTheme);
    setThemeState(nextTheme);
    setResolvedTheme(nextResolvedTheme);
    applyResolvedTheme(nextResolvedTheme);
  }, []);

  useEffect(() => {
    if (theme !== "system") {
      return;
    }

    const mediaQuery = window.matchMedia(SYSTEM_THEME_QUERY);
    const handleSystemThemeChange = (event: MediaQueryListEvent) => {
      const nextResolvedTheme = event.matches ? "dark" : "light";
      setResolvedTheme(nextResolvedTheme);
      applyResolvedTheme(nextResolvedTheme);
    };

    mediaQuery.addEventListener("change", handleSystemThemeChange);
    return () => {
      mediaQuery.removeEventListener("change", handleSystemThemeChange);
    };
  }, [theme]);

  useEffect(() => {
    const handleStorage = (event: StorageEvent) => {
      if (event.key !== STORAGE_KEY) {
        return;
      }

      const nextTheme = isThemePreference(event.newValue)
        ? event.newValue
        : "system";
      const nextResolvedTheme = resolveTheme(nextTheme);

      setThemeState(nextTheme);
      setResolvedTheme(nextResolvedTheme);
      applyResolvedTheme(nextResolvedTheme);
      document.cookie = `${COOKIE_KEY}=${nextTheme}; Path=/; Max-Age=${COOKIE_MAX_AGE}; SameSite=Lax; Secure`;
    };

    window.addEventListener("storage", handleStorage);
    return () => window.removeEventListener("storage", handleStorage);
  }, []);

  const contextValue = useMemo(
    () => ({ theme, resolvedTheme, setTheme }),
    [resolvedTheme, setTheme, theme],
  );

  return (
    <ThemeContext.Provider value={contextValue}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const context = useContext(ThemeContext);

  if (!context) {
    throw new Error("useTheme must be used within ThemeProvider");
  }

  return context;
}
