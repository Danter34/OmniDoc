import { apiRequest } from "@/services/api-client";
import type {
  AuthResponse,
  EmailVerificationOtpResponse,
  LoginRequest,
  RegisterRequest,
  User,
} from "@/types/auth.types";

export const authService = {
  login(payload: LoginRequest) {
    return apiRequest<AuthResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },

  register(payload: RegisterRequest) {
    return apiRequest<AuthResponse>("/api/auth/register", {
      method: "POST",
      body: JSON.stringify(payload),
    });
  },

  getCurrentUser() {
    return apiRequest<User>("/api/auth/me");
  },

  sendVerificationOtp() {
    return apiRequest<EmailVerificationOtpResponse>(
      "/api/auth/send-verification-otp",
      { method: "POST" },
    );
  },

  verifyEmail(otp: string) {
    return apiRequest<User>("/api/auth/verify-email", {
      method: "POST",
      body: JSON.stringify({ otp }),
    });
  },
};
