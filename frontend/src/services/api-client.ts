import { tokenStorage } from "@/services/token-storage";

const configuredApiUrl = process.env.NEXT_PUBLIC_API_URL?.trim();
export const API_BASE_URL = configuredApiUrl?.replace(/\/$/, "") ?? "";
export const UNAUTHORIZED_EVENT = "omnidoc:unauthorized";

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly errors: string[] = [],
  ) {
    super(message);
    this.name = "ApiError";
  }
}

function getRequestUrl(path: string) {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  return `${API_BASE_URL}${path.startsWith("/") ? path : `/${path}`}`;
}

export function getRealtimeUrl(path: string) {
  const requestUrl = getRequestUrl(path);

  if (/^https?:\/\//i.test(requestUrl)) {
    return requestUrl;
  }

  if (typeof window === "undefined") {
    return requestUrl;
  }

  return new URL(requestUrl, window.location.origin).toString();
}

async function readResponseBody(response: Response) {
  if (response.status === 204) {
    return null;
  }

  const contentType = response.headers.get("content-type") ?? "";

  if (contentType.includes("application/json")) {
    return response.json();
  }

  const text = await response.text();
  return text || null;
}

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  const token = tokenStorage.get();

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (init.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(getRequestUrl(path), {
    ...init,
    headers,
  });
  const body: unknown = await readResponseBody(response);

  if (!response.ok) {
    const errors =
      body &&
      typeof body === "object" &&
      "errors" in body &&
      Array.isArray(body.errors)
        ? body.errors.filter(
            (item: unknown): item is string => typeof item === "string",
          )
        : [];
    const fallbackMessage =
      typeof body === "string" && body.trim()
        ? body
        : `Yêu cầu thất bại (${response.status}).`;

    if (response.status === 401 && typeof window !== "undefined") {
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
    }

    throw new ApiError(errors[0] ?? fallbackMessage, response.status, errors);
  }

  return body as T;
}

export function getErrorMessage(error: unknown) {
  if (error instanceof ApiError || error instanceof Error) {
    return error.message;
  }

  return "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại.";
}
