"use client";

import {
  createContext,
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";

import {
  UNAUTHORIZED_EVENT,
  getErrorMessage,
} from "@/services/api-client";
import { authService } from "@/services/auth.service";
import { tokenStorage } from "@/services/token-storage";
import type {
  ChangePasswordRequest,
  LoginRequest,
  RegisterRequest,
  User,
} from "@/types/auth.types";

interface AuthContextValue {
  user: User | null;
  token: string | null;
  isLoading: boolean;
  login: (payload: LoginRequest) => Promise<void>;
  register: (payload: RegisterRequest) => Promise<void>;
  logout: () => void;
  refreshUser: () => Promise<void>;
  changePassword: (payload: ChangePasswordRequest) => Promise<void>;
  verificationModalOpen: boolean;
  openVerificationModal: () => void;
  closeVerificationModal: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [verificationModalOpen, setVerificationModalOpen] = useState(false);

  const clearSession = useCallback(() => {
    tokenStorage.clear();
    setToken(null);
    setUser(null);
    setVerificationModalOpen(false);
  }, []);

  const refreshUser = useCallback(async () => {
    const storedToken = tokenStorage.get();

    if (!storedToken) {
      clearSession();
      return;
    }

    setToken(storedToken);

    try {
      setUser(await authService.getCurrentUser());
    } catch {
      clearSession();
      throw new Error("Phiên đăng nhập đã hết hạn.");
    }
  }, [clearSession]);

  useEffect(() => {
    let active = true;

    async function restoreSession() {
      try {
        await refreshUser();
      } catch {
        // An invalid persisted token is already cleared by refreshUser.
      } finally {
        if (active) {
          setIsLoading(false);
        }
      }
    }

    void restoreSession();

    const handleUnauthorized = () => {
      clearSession();
      setIsLoading(false);
    };

    window.addEventListener(UNAUTHORIZED_EVENT, handleUnauthorized);

    return () => {
      active = false;
      window.removeEventListener(UNAUTHORIZED_EVENT, handleUnauthorized);
    };
  }, [clearSession, refreshUser]);

  const login = useCallback(async (payload: LoginRequest) => {
    const response = await authService.login(payload);
    tokenStorage.set(response.token);
    setToken(response.token);
    setUser({
      id: response.id,
      email: response.email,
      fullName: response.fullName,
      createdAtUtc: response.createdAtUtc,
      emailConfirmed: response.emailConfirmed,
      otpResendAvailableAt: response.otpResendAvailableAt,
    });
  }, []);

  const register = useCallback(async (payload: RegisterRequest) => {
    const response = await authService.register(payload);
    tokenStorage.set(response.token);
    setToken(response.token);
    setUser({
      id: response.id,
      email: response.email,
      fullName: response.fullName,
      createdAtUtc: response.createdAtUtc,
      emailConfirmed: response.emailConfirmed,
      otpResendAvailableAt: response.otpResendAvailableAt,
    });
  }, []);

  const changePassword = useCallback(async (payload: ChangePasswordRequest) => {
    const response = await authService.changePassword(payload);
    tokenStorage.set(response.token);
    setToken(response.token);
    setUser({
      id: response.id,
      email: response.email,
      fullName: response.fullName,
      createdAtUtc: response.createdAtUtc,
      emailConfirmed: response.emailConfirmed,
      otpResendAvailableAt: response.otpResendAvailableAt,
    });
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      token,
      isLoading,
      login,
      register,
      logout: clearSession,
      refreshUser: async () => {
        try {
          await refreshUser();
        } catch (error) {
          throw new Error(getErrorMessage(error));
        }
      },
      changePassword,
      verificationModalOpen,
      openVerificationModal: () => setVerificationModalOpen(true),
      closeVerificationModal: () => setVerificationModalOpen(false),
    }),
    [
      clearSession,
      changePassword,
      isLoading,
      login,
      refreshUser,
      register,
      token,
      user,
      verificationModalOpen,
    ],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
