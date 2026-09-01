export interface User {
  id: string;
  email: string;
  fullName: string;
  createdAtUtc?: string;
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
