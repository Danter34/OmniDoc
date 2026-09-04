export interface User {
  id: string;
  email: string;
  fullName: string;
  createdAtUtc?: string;
  emailConfirmed: boolean;
  otpResendAvailableAt?: string | null;
}

export interface AuthResponse extends User {
  token: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest extends LoginRequest {
  fullName: string;
}

export interface EmailVerificationOtpResponse {
  success: boolean;
  resendCooldownSeconds: number;
  debugOtp: string | null;
  expiresAt: string;
  resendAvailableAt: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ForgotPasswordResponse {
  message: string;
  debugResetUrl: string | null;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface PasswordResetResponse {
  message: string;
}
