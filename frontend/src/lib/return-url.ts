export function safeReturnUrl(value?: string): string {
  if (!value?.startsWith("/") || value.startsWith("//") || /[\\\u0000-\u001f\u007f]/.test(value)) {
    return "/workspaces";
  }

  return value;
}
