// Public demo credentials only. The showcase seeder must use the same values.
// NEXT_PUBLIC_* values are embedded at build time and are not secrets.
export const showcase = {
  enabled: process.env.NEXT_PUBLIC_SHOWCASE_ENABLED !== "false",
  email: process.env.NEXT_PUBLIC_SHOWCASE_EMAIL || "guest@omnidoc.io",
  password: process.env.NEXT_PUBLIC_SHOWCASE_PASSWORD || "OmniDoc-Showcase2026!",
  workspaceId: process.env.NEXT_PUBLIC_SHOWCASE_WORKSPACE_ID?.trim() || null,
};

export function isShowcaseUser(user: { email: string } | null) {
  return showcase.enabled && user?.email.trim().toLowerCase() === showcase.email.trim().toLowerCase();
}

export function getShowcaseWorkspaceId(workspaces: { id: string; role: string }[]) {
  return showcase.workspaceId ?? workspaces.find((workspace) => workspace.role === "Owner")?.id ?? null;
}

export const SHOWCASE_PROMPTS = [
  "Dự báo tăng trưởng GDP và tỷ lệ lạm phát của Việt Nam trong giai đoạn 2024–2025 là bao nhiêu?",
  "Sự phụ thuộc vào đồng Đô la Mỹ (USD) đặt ra những rủi ro trọng yếu nào cho hệ thống tài chính ASEAN+3?",
  "Thách thức từ già hóa dân số đối với khu vực ASEAN+3 là gì và công nghệ hỗ trợ ra sao?",
];

export const SHOWCASE_DEFAULT_DOCUMENT = "Highlights-Booklet_Vietnamese.pdf";
