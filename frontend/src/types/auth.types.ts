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
  expiresAt: string;
  resendAvailableAt: string;
}
